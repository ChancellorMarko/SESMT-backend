using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SESMT.API.Contracts.Users;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.IdentityAccess.Constants;

namespace SESMT.API.Controllers;

public sealed partial class UsersController
{
    [HttpGet("management-context")]
    public async Task<IActionResult> ManagementContext([FromQuery] string action, [FromQuery] Guid? userId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryGetActor(out var actorId)) return Failure(401, "INVALID_CLAIMS", "Usuário não identificado.");
        if (action is not ("criar" or "editar") || (action == "editar") != userId.HasValue)
            return Failure(400, "VALIDATION_FAILED", "Informe a ação e, na edição, a conta de destino.");
        var allowed = await GetAdminScopesAsync(actorId, action, ct);
        if (allowed is { Count: 0 }) return Failure(403, "FORBIDDEN", "Sem autorização para administrar estes acessos.");
        UserResponse? target = null;
        string? version = null;
        if (userId.HasValue)
        {
            var user = await FindManagedUserAsync(userId.Value, actorId, action, ct);
            if (user is null) return Failure(404, "NOT_FOUND", "Conta não encontrada ou fora do seu escopo.");
            if (!user.Active || user.Id == actorId || await HasGlobalAssignmentAsync(user.Id, ct))
                return Failure(409, "PROTECTED_ACCOUNT", "Esta conta não pode ter seus acessos alterados por este formulário.");
            target = await ToResponseAsync(user, ct);
            version = await AccessVersionAsync(user.Id, ct);
        }
        var companies = await _db.Companies.Where(c => c.Active && c.DeletedAt == null)
            .Select(c => new { c.Id, c.FantasyName }).ToListAsync(ct);
        var units = await _db.Units.Where(u => u.Active && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Name, u.CompanyId }).ToListAsync(ct);
        var departments = await _db.Departments.Where(d => d.Active && d.DeletedAt == null)
            .Select(d => new { d.Id, d.Name, d.UnitId }).ToListAsync(ct);
        var scopes = new List<UserScopeOption>();
        foreach (var company in companies)
        {
            Add(new(company.Id, ScopeType.EMPRESA, company.FantasyName, company.Id, company.FantasyName));
            foreach (var unit in units.Where(u => u.CompanyId == company.Id))
            {
                Add(new(unit.Id, ScopeType.UNIDADE, unit.Name, company.Id, company.FantasyName));
                foreach (var department in departments.Where(d => d.UnitId == unit.Id))
                    Add(new(department.Id, ScopeType.SETOR, $"{unit.Name} / {department.Name}", company.Id, company.FantasyName));
            }
        }
        void Add(UserScopeOption option)
        {
            if (allowed is null || allowed.Any(a => Contains(a, new(option.CompanyId, option.Type, option.Id)))) scopes.Add(option);
        }
        // The existing delegation policy permits any active custom role, within authorized scopes.
        var roles = await _db.Roles.Where(r => r.Active && r.DeletedAt == null).OrderBy(r => r.Name)
            .Select(r => new UserRoleOption(r.Code, r.Name, r.Description)).ToListAsync(ct);
        return Ok(new UserManagementContext(action, target, version, roles, scopes));
    }

    [HttpPut("{id:guid}/access")]
    public async Task<IActionResult> SetAccess(Guid id, [FromBody] UserAccessRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return Failure(401, "INVALID_CLAIMS", "Usuário não identificado.");
        if (!request.Confirmation || request.Roles is null || request.Roles.Count is 0 or > 100
            || request.ScopeIds is null || request.ScopeIds.Count is 0 or > 500
            || request.ExpectedVersion is null || request.ExpectedVersion.Length != 64)
            return Failure(400, "VALIDATION_FAILED", "Confirme os perfis, escopos e a versão consultada.");
        await using var transaction = await LockManagedUserAsync(id, ct);
        var user = await FindManagedUserAsync(id, actorId, "editar", ct);
        if (user is null) return Failure(404, "NOT_FOUND", "Conta não encontrada ou fora do seu escopo.");
        if (!user.Active || id == actorId || await HasGlobalAssignmentAsync(id, ct))
            return Failure(409, "PROTECTED_ACCOUNT", "Esta conta não pode ter seus acessos alterados por este formulário.");
        if (request.ExpectedVersion != await AccessVersionAsync(id, ct))
            return Failure(409, "ACCESS_CHANGED", "Os acessos foram alterados. Consulte a conta novamente.");
        var scopes = await ResolveAndAuthorizeScopesAsync(actorId, request.ScopeIds, "editar", ct);
        if (scopes is null) return Failure(403, "FORBIDDEN", "Os locais selecionados não podem ser atribuídos.");
        var roles = await ResolveRolesAsync(request.Roles, actorId, ct);
        if (roles is null || roles.Count == 0) return Failure(400, "INVALID_ROLES", "Selecione perfis ativos válidos.");
        var oldScopes = await UserScopesAsync(id, ct);
        var before = AuditSnapshot(user.Name, user.Email.Value, await RoleCodesAsync(id, ct), oldScopes);
        var assignments = await _db.UserRoles.Where(r => r.UserId == id && r.DeletedAt == null).ToListAsync(ct);
        var scopeAssignments = await _db.UserScopes.Where(s => s.UserId == id && s.DeletedAt == null).ToListAsync(ct);
        foreach (var assignment in assignments) assignment.MarkAsDeleted(actorId);
        foreach (var assignment in scopeAssignments) assignment.MarkAsDeleted(actorId);
        AddScopes(id, scopes, actorId);
        AddRoles(id, roles, scopes.Select(s => s.CompanyId).Distinct(), actorId);
        await AddAuditAsync(AuditAction.PermissionChange, id, actorId, before,
            AuditSnapshot(user.Name, user.Email.Value, request.Roles, scopes), oldScopes.Concat(scopes).Select(s => s.CompanyId), ct);
        await _db.SaveChangesAsync(ct);
        var response = new UserAccessResponse(await ToResponseAsync(user, ct), await AccessVersionAsync(id, ct));
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(response);
    }

    private Task<bool> HasGlobalAssignmentAsync(Guid userId, CancellationToken ct) => _db.UserRoles.AnyAsync(r =>
        r.UserId == userId && r.CompanyId == null && r.Active && r.DeletedAt == null, ct);

    private async Task<string> AccessVersionAsync(Guid userId, CancellationToken ct)
    {
        var roles = await _db.UserRoles.Where(r => r.UserId == userId && r.Active && r.DeletedAt == null)
            .OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);
        var scopes = await _db.UserScopes.Where(s => s.UserId == userId && s.Active && s.DeletedAt == null)
            .OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { roles, scopes })))).ToLowerInvariant();
    }

    private async Task<IDbContextTransaction?> LockManagedUserAsync(Guid id, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return null;
        var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM core.usuario WHERE id = {id} FOR UPDATE", ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
}
