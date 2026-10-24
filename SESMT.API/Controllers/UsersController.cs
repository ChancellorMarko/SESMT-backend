using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Contracts.Users;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private const string UserResource = "usuario";
    private readonly SesmtDbContext _db;
    private readonly IPasswordHashService _passwordHasher;

    public UsersController(SesmtDbContext db, IPasswordHashService passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email)
            || request.Roles is null || request.ScopeIds is null || request.ScopeIds.Count == 0)
            return BadRequest(new { error = "Nome, e-mail, perfis e ao menos um escopo são obrigatórios." });

        var scopes = await ResolveAndAuthorizeScopesAsync(actorId, request.ScopeIds, "criar", ct);
        if (scopes is null) return Forbid();
        var roles = await ResolveRolesAsync(request.Roles, actorId, ct);
        if (roles is null) return BadRequest(new { error = "Um ou mais perfis não existem ou não podem ser atribuídos por este administrador." });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email.Value == normalizedEmail && u.DeletedAt == null, ct))
            return Conflict(new { error = "Já existe uma conta com este e-mail." });

        // A senha aleatória não é retornada nem registrada; a pessoa define a própria senha pelo fluxo de recuperação.
        var unusablePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var user = new User(request.Name, normalizedEmail, _passwordHasher.HashPassword(unusablePassword));
        await _db.Users.AddAsync(user, ct);
        AddScopes(user.Id, scopes, actorId);
        AddRoles(user.Id, roles, scopes.Select(s => s.CompanyId).Distinct(), actorId);
        await AddAuditAsync(AuditAction.Create, user.Id, actorId, null, AuditSnapshot(user.Name, normalizedEmail, request.Roles, scopes), scopes.Select(s => s.CompanyId).Distinct(), ct);
        await _db.SaveChangesAsync(ct);

        return Created($"/users/{user.Id}", await ToResponseAsync(user, ct));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var accessible = await GetAdminScopesAsync(actorId, "ler", ct);
        if (accessible is null)
        {
            var allIds = await _db.Users.Where(u => u.DeletedAt == null).OrderBy(u => u.Name).Select(u => u.Id).ToListAsync(ct);
            return Ok(await ResponsesAsync(allIds, ct));
        }
        if (accessible.Count == 0) return Forbid();

        var candidates = await _db.UserScopes.Where(s => s.Active && s.DeletedAt == null)
            .Select(s => s.UserId).Distinct().ToListAsync(ct);
        var visibleUserIds = new List<Guid>();
        foreach (var candidateId in candidates)
            if ((await UserScopesAsync(candidateId, ct)).Any(target => accessible.Any(allowed => Contains(allowed, target))))
                visibleUserIds.Add(candidateId);
        return Ok(await ResponsesAsync(visibleUserIds, ct));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Nome e e-mail são obrigatórios." });

        var user = await FindManagedUserAsync(id, actorId, "editar", ct);
        if (user is null) return NotFound();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Id != id && u.Email.Value == email && u.DeletedAt == null, ct))
            return Conflict(new { error = "Já existe uma conta com este e-mail." });

        var before = AuditSnapshot(user.Name, user.Email.Value, await RoleCodesAsync(id, ct), await UserScopesAsync(id, ct));
        user.Update(request.Name, email);
        await AddAuditAsync(AuditAction.Update, user.Id, actorId, before, AuditSnapshot(user.Name, email, await RoleCodesAsync(id, ct), await UserScopesAsync(id, ct)), await CompanyIdsForUserAsync(id, ct), ct);
        await _db.SaveChangesAsync(ct);
        return Ok(await ToResponseAsync(user, ct));
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] UserRolesRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (request.Roles is null) return BadRequest(new { error = "A lista de perfis é obrigatória." });
        var user = await FindManagedUserAsync(id, actorId, "editar", ct);
        if (user is null) return NotFound();
        var scopes = await UserScopesAsync(id, ct);
        if (scopes.Count == 0) return Conflict(new { error = "A conta não possui escopo ativo." });
        var roles = await ResolveRolesAsync(request.Roles, actorId, ct);
        if (roles is null) return BadRequest(new { error = "Um ou mais perfis não existem ou não podem ser atribuídos por este administrador." });

        var before = AuditSnapshot(user.Name, user.Email.Value, await RoleCodesAsync(id, ct), scopes);
        var existing = await _db.UserRoles.Where(r => r.UserId == id && r.DeletedAt == null).ToListAsync(ct);
        foreach (var role in existing) role.MarkAsDeleted(actorId);
        AddRoles(id, roles, scopes.Select(s => s.CompanyId).Distinct(), actorId);
        await AddAuditAsync(AuditAction.PermissionChange, id, actorId, before, AuditSnapshot(user.Name, user.Email.Value, request.Roles, scopes), scopes.Select(s => s.CompanyId).Distinct(), ct);
        await _db.SaveChangesAsync(ct);
        return Ok(await ToResponseAsync(user, ct));
    }

    [HttpPost("{id:guid}/scopes")]
    public async Task<IActionResult> SetScopes(Guid id, [FromBody] UserScopesRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (request.ScopeIds is null || request.ScopeIds.Count == 0)
            return BadRequest(new { error = "Informe ao menos um escopo ativo." });
        var user = await FindManagedUserAsync(id, actorId, "editar", ct);
        if (user is null) return NotFound();
        var scopes = await ResolveAndAuthorizeScopesAsync(actorId, request.ScopeIds, "editar", ct);
        if (scopes is null) return Forbid();
        var beforeScopes = await UserScopesAsync(id, ct);
        var before = AuditSnapshot(user.Name, user.Email.Value, await RoleCodesAsync(id, ct), beforeScopes);
        var existing = await _db.UserScopes.Where(s => s.UserId == id && s.DeletedAt == null).ToListAsync(ct);
        foreach (var scope in existing) scope.MarkAsDeleted(actorId);
        AddScopes(id, scopes, actorId);
        await AddAuditAsync(AuditAction.PermissionChange, id, actorId, before, AuditSnapshot(user.Name, user.Email.Value, await RoleCodesAsync(id, ct), scopes), scopes.Select(s => s.CompanyId).Distinct(), ct);
        await _db.SaveChangesAsync(ct);
        return Ok(await ToResponseAsync(user, ct));
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (id == actorId) return Conflict(new { error = "Não é possível desativar a própria conta." });
        var user = await FindManagedUserAsync(id, actorId, "excluir", ct);
        if (user is null) return NotFound();
        var scopes = await UserScopesAsync(id, ct);
        user.Deactivate(actorId);
        await AddAuditAsync(AuditAction.Delete, id, actorId, null, AuditSnapshot(user.Name, user.Email.Value, await RoleCodesAsync(id, ct), scopes), scopes.Select(s => s.CompanyId).Distinct(), ct);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private bool TryGetActor(out Guid actorId)
    {
        var value = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out actorId);
    }

    private async Task<User?> FindManagedUserAsync(Guid id, Guid actorId, string action, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, ct);
        if (user is null) return null;
        var targetScopes = await UserScopesAsync(id, ct);
        var access = await GetAdminScopesAsync(actorId, action, ct);
        if (access is null) return user; // administrador global
        if (access.Count == 0 || !targetScopes.Any(target => access.Any(allowed => Contains(allowed, target)))) return null;
        return user;
    }

    private async Task<List<ManagedScope>?> GetAdminScopesAsync(Guid actorId, string action, CancellationToken ct)
    {
        var query = from assignment in _db.UserRoles
                    join role in _db.Roles on assignment.RoleId equals role.Id
                    join rolePermission in _db.RolePermissions on role.Id equals rolePermission.RoleId
                    join permission in _db.Permissions on rolePermission.PermissionId equals permission.Id
                    where assignment.UserId == actorId && assignment.Active && assignment.DeletedAt == null
                        && role.Active && role.DeletedAt == null && role.Code == "ADMIN"
                        && rolePermission.Active && rolePermission.DeletedAt == null
                        && permission.Active && permission.DeletedAt == null
                        && permission.Resource == UserResource && permission.Action == action
                    select assignment.CompanyId;
        var roleCompanies = await query.Distinct().ToListAsync(ct);
        if (roleCompanies.Contains(null)) return null;
        if (roleCompanies.Count == 0) return [];

        var userScopes = await _db.UserScopes.Where(s => s.UserId == actorId && s.Active && s.DeletedAt == null && s.ReferenceId.HasValue)
            .Select(s => new { s.ScopeType, ReferenceId = s.ReferenceId!.Value }).ToListAsync(ct);
        var companies = new List<ManagedScope>();
        foreach (var scope in userScopes)
        {
            if (scope.ScopeType == ScopeType.EMPRESA && roleCompanies.Contains(scope.ReferenceId))
                companies.Add(new(scope.ReferenceId, ScopeType.EMPRESA, scope.ReferenceId));
            else if (scope.ScopeType == ScopeType.UNIDADE)
            {
                var unit = await _db.Units.Where(u => u.Id == scope.ReferenceId && u.Active && u.DeletedAt == null)
                    .Select(u => new { u.Id, u.CompanyId }).FirstOrDefaultAsync(ct);
                if (unit is not null && roleCompanies.Contains(unit.CompanyId)) companies.Add(new(unit.CompanyId, ScopeType.UNIDADE, unit.Id));
            }
            else if (scope.ScopeType == ScopeType.SETOR)
            {
                var department = await (from d in _db.Departments join u in _db.Units on d.UnitId equals u.Id
                    where d.Id == scope.ReferenceId && d.Active && d.DeletedAt == null && u.Active && u.DeletedAt == null
                    select new { DepartmentId = d.Id, u.CompanyId }).FirstOrDefaultAsync(ct);
                if (department is not null && roleCompanies.Contains(department.CompanyId)) companies.Add(new(department.CompanyId, ScopeType.SETOR, department.DepartmentId));
            }
        }
        return companies.Distinct().ToList();
    }

    private async Task<List<ManagedScope>?> ResolveAndAuthorizeScopesAsync(Guid actorId, IReadOnlyList<Guid> ids, string action, CancellationToken ct)
    {
        var allowed = await GetAdminScopesAsync(actorId, action, ct);
        if (allowed is { Count: 0 }) return null;
        var scopes = new List<ManagedScope>();
        foreach (var id in ids.Distinct())
        {
            var company = await _db.Companies.Where(c => c.Id == id && c.Active && c.DeletedAt == null).Select(c => c.Id).FirstOrDefaultAsync(ct);
            ManagedScope? scope = company != Guid.Empty ? new(company, ScopeType.EMPRESA, id) : null;
            if (scope is null)
            {
                var unit = await _db.Units.Where(u => u.Id == id && u.Active && u.DeletedAt == null).Select(u => new { u.Id, u.CompanyId }).FirstOrDefaultAsync(ct);
                if (unit is not null) scope = new(unit.CompanyId, ScopeType.UNIDADE, unit.Id);
            }
            if (scope is null)
            {
                var department = await (from d in _db.Departments join u in _db.Units on d.UnitId equals u.Id
                    where d.Id == id && d.Active && d.DeletedAt == null && u.Active && u.DeletedAt == null
                    select new { DepartmentId = d.Id, u.CompanyId }).FirstOrDefaultAsync(ct);
                if (department is not null) scope = new(department.CompanyId, ScopeType.SETOR, department.DepartmentId);
            }
            if (scope is null || (allowed is not null && !allowed.Any(a => Contains(a, scope)))) return null;
            scopes.Add(scope);
        }
        return scopes;
    }

    private async Task<List<Role>?> ResolveRolesAsync(IReadOnlyList<string> codes, Guid actorId, CancellationToken ct)
    {
        var normalized = codes.Select(c => c?.Trim().ToUpperInvariant()).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToArray();
        var roles = await _db.Roles.Where(r => normalized.Contains(r.Code) && r.Active && r.DeletedAt == null).ToListAsync(ct);
        if (roles.Count != normalized.Length) return null;
        var globalAdmin = await _db.UserRoles.AnyAsync(ur => ur.UserId == actorId && ur.CompanyId == null && ur.Active && ur.DeletedAt == null
            && _db.Roles.Any(r => r.Id == ur.RoleId && r.Code == "ADMIN" && r.Active && r.DeletedAt == null), ct);
        if (!globalAdmin && roles.Any(r => r.Code is "ADMIN" or "GESTOR" or "TECNICO" or "MEDICO")) return null;
        return roles;
    }

    private void AddScopes(Guid userId, IEnumerable<ManagedScope> scopes, Guid actorId)
    {
        foreach (var scope in scopes)
        {
            var entity = new UserScope(userId, scope.ScopeType);
            entity.Update(userId, scope.ScopeType, scope.ReferenceId);
            _db.UserScopes.Add(entity);
        }
    }

    private void AddRoles(Guid userId, IReadOnlyCollection<Role> roles, IEnumerable<Guid> companyIds, Guid actorId)
    {
        foreach (var companyId in companyIds)
            foreach (var role in roles)
                _db.UserRoles.Add(new UserRole(userId, role.Id, companyId));
    }

    private async Task<List<UserResponse>> ResponsesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var users = await _db.Users.Where(u => ids.Contains(u.Id) && u.DeletedAt == null).OrderBy(u => u.Name).ToListAsync(ct);
        var responses = new List<UserResponse>();
        foreach (var user in users) responses.Add(await ToResponseAsync(user, ct));
        return responses;
    }

    private async Task<UserResponse> ToResponseAsync(User user, CancellationToken ct) =>
        new(user.Id, user.Name, user.Email.Value, await RoleCodesAsync(user.Id, ct),
            (await UserScopesAsync(user.Id, ct)).Select(s => s.ReferenceId).Distinct().ToArray(), user.Active && user.DeletedAt is null);

    private async Task<IReadOnlyList<string>> RoleCodesAsync(Guid userId, CancellationToken ct) => await (
        from assignment in _db.UserRoles join role in _db.Roles on assignment.RoleId equals role.Id
        where assignment.UserId == userId && assignment.Active && assignment.DeletedAt == null && role.Active && role.DeletedAt == null
        select role.Code).Distinct().OrderBy(code => code).ToListAsync(ct);

    private async Task<List<ManagedScope>> UserScopesAsync(Guid userId, CancellationToken ct)
    {
        var scopes = await _db.UserScopes.Where(s => s.UserId == userId && s.Active && s.DeletedAt == null && s.ReferenceId.HasValue)
            .Select(s => new { s.ScopeType, ReferenceId = s.ReferenceId!.Value }).ToListAsync(ct);
        var result = new List<ManagedScope>();
        foreach (var scope in scopes)
        {
            if (scope.ScopeType == ScopeType.EMPRESA) result.Add(new(scope.ReferenceId, scope.ScopeType, scope.ReferenceId));
            else if (scope.ScopeType == ScopeType.UNIDADE)
            {
                var companyId = await _db.Units.Where(u => u.Id == scope.ReferenceId).Select(u => u.CompanyId).FirstOrDefaultAsync(ct);
                if (companyId != Guid.Empty) result.Add(new(companyId, scope.ScopeType, scope.ReferenceId));
            }
            else if (scope.ScopeType == ScopeType.SETOR)
            {
                var companyId = await (from d in _db.Departments join u in _db.Units on d.UnitId equals u.Id where d.Id == scope.ReferenceId select u.CompanyId).FirstOrDefaultAsync(ct);
                if (companyId != Guid.Empty) result.Add(new(companyId, scope.ScopeType, scope.ReferenceId));
            }
        }
        return result;
    }

    private async Task<List<Guid>> CompanyIdsForUserAsync(Guid userId, CancellationToken ct) => (await UserScopesAsync(userId, ct)).Select(s => s.CompanyId).Distinct().ToList();

    private async Task AddAuditAsync(string action, Guid resourceId, Guid actorId, string? previous, string? next, IEnumerable<Guid> companyIds, CancellationToken ct)
    {
        var lastHash = await _db.AuditLogs.OrderByDescending(log => log.Id).Select(log => log.CurrentHash).FirstOrDefaultAsync(ct);
        var context = JsonSerializer.Serialize(new { companyIds = companyIds.Distinct().OrderBy(id => id) });
        var log = new AuditLog(action, "usuario", resourceId, actorId, null, null, previous, next, context,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), lastHash);
        _db.AuditLogs.Add(log);
    }

    private static string AuditSnapshot(string name, string email, IEnumerable<string> roles, IEnumerable<ManagedScope> scopes) =>
        JsonSerializer.Serialize(new { name, email, roles, scopes = scopes.Select(s => new { s.ScopeType, s.ReferenceId }) });

    private static bool Contains(ManagedScope allowed, ManagedScope target) =>
        allowed.CompanyId == target.CompanyId && (allowed.ScopeType == ScopeType.EMPRESA
            || (allowed.ScopeType == target.ScopeType && allowed.ReferenceId == target.ReferenceId));

    private sealed record ManagedScope(Guid CompanyId, string ScopeType, Guid ReferenceId);
}
