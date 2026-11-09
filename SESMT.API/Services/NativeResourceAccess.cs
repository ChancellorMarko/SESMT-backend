using Microsoft.EntityFrameworkCore;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

/// <summary>Resource authorization at the native API service boundary. Reuses the browser's
/// current-database hierarchy without inferring grants from JWT role names.</summary>
public sealed class NativeResourceAccess(
    SesmtDbContext db, ICurrentUserService currentUser, IBrowserWorkspaceAccessReader reader)
{
    private Task<BrowserWorkspaceAccess?>? _snapshot;

    public async Task<BrowserWorkspaceAccess> ReadAsync(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid actor)
            throw new UnauthorizedAccessException("Não foi possível identificar o usuário autenticado.");
        return await (_snapshot ??= reader.ReadNativeAsync(actor, ct)) ?? throw new ResourceForbiddenException();
    }

    public static Guid[] Companies(BrowserWorkspaceAccess access, string permission)
        => access.Grants.Where(g => g.Permission == permission && access.CompanyIds.Contains(g.CompanyId))
            .Select(g => g.CompanyId).Distinct().ToArray();

    public static void Require(BrowserWorkspaceAccess access, string permission, Guid company, bool entireCompany = false)
    {
        if (!access.Can(permission, company) || entireCompany && !access.IsFullCompany(company)
            || access.OwnOnly && !IsRead(permission))
            throw new ResourceForbiddenException();
    }

    /// <summary>Read-only permissions. aso:acompanhar and aso:detalhar only consult the health
    /// registry, so a worker with own-only scope may use them for their own records.</summary>
    private static bool IsRead(string permission) => permission.EndsWith(":ler", StringComparison.Ordinal)
        || permission is "aso:acompanhar" or "aso:detalhar";

    public async Task<BrowserWorkspaceAccess> RequireAsync(string permission, Guid company, bool entireCompany, CancellationToken ct)
    {
        var access = await ReadAsync(ct);
        Require(access, permission, company, entireCompany);
        return access;
    }

    public async Task<IQueryable<Worker>> WorkersAsync(string action, CancellationToken ct)
    {
        var access = await ReadAsync(ct);
        var companies = Companies(access, "trabalhador:" + action);
        if (companies.Length == 0 || access.OwnOnly && action != "ler") throw new ResourceForbiddenException();
        var fullCompanies = access.FullCompanyIds.ToArray();
        var fullUnits = access.FullUnitIds.ToArray();
        var sectors = access.SectorIds.ToArray();
        return db.Workers.Where(w => w.DeletedAt == null && companies.Contains(w.CompanyId)
            && (!access.OwnOnly || w.Id == access.WorkerId)
            && (!w.UnitId.HasValue && !w.DepartmentId.HasValue && fullCompanies.Contains(w.CompanyId)
                || w.UnitId.HasValue && db.Units.Any(u => u.Id == w.UnitId && u.CompanyId == w.CompanyId && u.Active && u.DeletedAt == null)
                    && (!w.DepartmentId.HasValue || db.Departments.Any(d => d.Id == w.DepartmentId && d.UnitId == w.UnitId && d.Active && d.DeletedAt == null))
                    && (fullUnits.Contains(w.UnitId.Value) || w.DepartmentId.HasValue && sectors.Contains(w.DepartmentId.Value))));
    }

    public async Task<Worker> WorkerAsync(Guid id, string action, CancellationToken ct)
        => await (await WorkersAsync(action, ct)).AsNoTracking().SingleOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Trabalhador não encontrado.");

    public async Task ValidateWorkerContextAsync(string action, Guid company, Guid? unit, Guid? sector, Guid? position, CancellationToken ct)
    {
        var access = await RequireAsync("trabalhador:" + action, company, false, ct);
        if (!access.AllowsLocation(company, unit, sector)) throw new ResourceForbiddenException();
        if (position.HasValue && !await db.Positions.AnyAsync(p => p.Id == position && p.CompanyId == company && p.Active && p.DeletedAt == null, ct))
            throw new ArgumentException("Cargo inválido para a empresa.");
    }

    public async Task ValidateAccountAsync(Guid? userId, Guid company, Guid? workerId, CancellationToken ct)
    {
        if (!userId.HasValue) return;
        var valid = await db.Users.AnyAsync(u => u.Id == userId && u.Active && u.DeletedAt == null, ct)
            && (await db.UserRoles.AnyAsync(r => r.UserId == userId && r.CompanyId == company && r.Active && r.DeletedAt == null, ct)
                || await db.UserScopes.AnyAsync(s => s.UserId == userId && s.Active && s.DeletedAt == null
                    && (s.ScopeType == "EMPRESA" && s.ReferenceId == company
                        || s.ScopeType == "UNIDADE" && db.Units.Any(u => u.Id == s.ReferenceId && u.CompanyId == company && u.Active && u.DeletedAt == null)
                        || s.ScopeType == "SETOR" && db.Departments.Any(d => d.Id == s.ReferenceId && d.Active && d.DeletedAt == null
                            && db.Units.Any(u => u.Id == d.UnitId && u.CompanyId == company && u.Active && u.DeletedAt == null))), ct));
        if (!valid || await db.Workers.AnyAsync(w => w.UserId == userId && w.Id != workerId && w.DeletedAt == null, ct))
            throw new ArgumentException("Conta inválida ou já vinculada a outro trabalhador.");
    }

    public async Task ValidateHistoryAsync(Worker worker, Guid sectorId, Guid positionId, CancellationToken ct)
    {
        var sector = await db.Departments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == sectorId && d.Active && d.DeletedAt == null, ct)
            ?? throw new ArgumentException("Setor inválido para a movimentação.");
        await ValidateWorkerContextAsync("editar", worker.CompanyId, sector.UnitId, sector.Id, positionId, ct);
    }

    public async Task<IQueryable<Team>> TeamsAsync(string action, CancellationToken ct)
    {
        var access = await ReadAsync(ct);
        var companies = Companies(access, "equipe:" + action);
        if (companies.Length == 0 || access.OwnOnly) throw new ResourceForbiddenException();
        var fullCompanies = access.FullCompanyIds.ToArray();
        var fullUnits = access.FullUnitIds.ToArray();
        return db.Teams.Where(t => t.Active && t.DeletedAt == null && t.CompanyId.HasValue && companies.Contains(t.CompanyId.Value)
            && (!t.LocalId.HasValue && fullCompanies.Contains(t.CompanyId.Value)
                || t.LocalId.HasValue && fullUnits.Contains(t.LocalId.Value)
                    && db.Units.Any(u => u.Id == t.LocalId && u.CompanyId == t.CompanyId && u.Active && u.DeletedAt == null)));
    }

    public async Task<Team> TeamAsync(Guid id, string action, CancellationToken ct)
        => await (await TeamsAsync(action, ct)).AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Equipe não encontrada.");

    public async Task ValidateTeamAsync(string action, Guid? company, Guid? unit, Guid? chief, IEnumerable<Guid> participants, CancellationToken ct)
    {
        if (!company.HasValue || !chief.HasValue) throw new ArgumentException("Informe empresa e chefe da equipe.");
        var access = await RequireAsync("equipe:" + action, company.Value, false, ct);
        if (access.OwnOnly || !access.AllowsLocation(company.Value, unit)
            || unit.HasValue && !access.FullUnitIds.Contains(unit.Value)) throw new ResourceForbiddenException();
        var ids = participants.Append(chief.Value).Distinct().ToArray();
        var workers = await db.Workers.AsNoTracking().Where(w => ids.Contains(w.Id) && w.CompanyId == company
                && w.Active && w.DeletedAt == null && w.DismissalDate == null).ToListAsync(ct);
        if (ids.Contains(Guid.Empty) || workers.Count != ids.Length
            || workers.Any(w => !access.AllowsLocation(w.CompanyId, w.UnitId, w.DepartmentId)))
            throw new ArgumentException("Chefe e participantes devem possuir vínculo ativo com a empresa.");
    }

    public async Task<Unit> UnitAsync(Guid id, string resource, string action, CancellationToken ct)
    {
        var unit = await db.Units.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.Active && u.DeletedAt == null, ct)
            ?? throw new NotFoundException("Unidade não encontrada.");
        var access = await RequireAsync(resource + ":" + action, unit.CompanyId, false, ct);
        if (!access.HasUnit(id) || !IsRead(resource + ":" + action) && !access.FullUnitIds.Contains(id)) throw new ResourceForbiddenException();
        return unit;
    }

    public async Task<Department> DepartmentAsync(Guid id, string action, CancellationToken ct)
    {
        var sector = await db.Departments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.Active && d.DeletedAt == null, ct)
            ?? throw new NotFoundException("Setor não encontrado.");
        var unit = await db.Units.AsNoTracking().SingleOrDefaultAsync(u => u.Id == sector.UnitId && u.Active && u.DeletedAt == null, ct)
            ?? throw new NotFoundException("Setor não encontrado.");
        var access = await RequireAsync("setor:" + action, unit.CompanyId, false, ct);
        if (!access.SectorIds.Contains(id) || access.OwnOnly) throw new ResourceForbiddenException();
        return sector;
    }

    public async Task<Position> PositionAsync(Guid id, string action, CancellationToken ct)
    {
        var position = await db.Positions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.Active && p.DeletedAt == null, ct)
            ?? throw new NotFoundException("Cargo não encontrado.");
        await RequireAsync("cargo:" + action, position.CompanyId, action != "ler", ct);
        return position;
    }

    public async Task RequireGlobalCompanyCreationAsync(CancellationToken ct)
    {
        var access = await ReadAsync(ct);
        var allowed = await (from a in db.UserRoles join r in db.Roles on a.RoleId equals r.Id
            join rp in db.RolePermissions on r.Id equals rp.RoleId join p in db.Permissions on rp.PermissionId equals p.Id
            where a.UserId == access.UserId && a.CompanyId == null && a.Active && a.DeletedAt == null
                && r.Code == "ADMIN" && r.Active && r.DeletedAt == null && rp.Active && rp.DeletedAt == null
                && p.Resource == "empresa" && p.Action == "criar" && p.Active && p.DeletedAt == null
            select a.Id).AnyAsync(ct);
        if (!allowed) throw new ResourceForbiddenException();
    }

    public static (int Skip, int Take) Page(int size, int number)
    {
        if (size is < 1 or > 200 || number is < 1 or > 1_000_000) throw new ArgumentException("Paginação inválida.");
        return ((number - 1) * size, size);
    }
}
