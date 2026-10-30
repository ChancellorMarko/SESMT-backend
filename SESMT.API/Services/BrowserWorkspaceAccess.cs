using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record BrowserWorkspaceGrant(string Permission, Guid CompanyId);

/// <summary>Current database grants and hierarchy. Ancestor unit IDs never broaden a sector grant.</summary>
public sealed record BrowserWorkspaceAccess(
    Guid UserId,
    IReadOnlyCollection<Guid> CompanyIds,
    IReadOnlyCollection<Guid> UnitIds,
    IReadOnlyCollection<Guid> SectorIds,
    IReadOnlyCollection<Guid> FullCompanyIds,
    IReadOnlyCollection<Guid> FullUnitIds,
    bool OwnOnly,
    Guid? WorkerId,
    IReadOnlyCollection<BrowserWorkspaceGrant> Grants,
    IReadOnlyDictionary<Guid, Guid>? UnitCompanies = null,
    IReadOnlyDictionary<Guid, Guid>? SectorUnits = null)
{
    public IReadOnlyCollection<string> Permissions => Grants.Select(grant => grant.Permission).Distinct().Order().ToArray();
    public bool Can(string permission, Guid companyId) => CompanyIds.Contains(companyId)
        && Grants.Any(grant => grant.CompanyId == companyId && grant.Permission == permission);
    public bool HasUnit(Guid unitId) => UnitIds.Contains(unitId);
    public bool IsFullCompany(Guid companyId) => FullCompanyIds.Contains(companyId);
    public bool AllowsLocation(Guid companyId, Guid? unitId, Guid? sectorId = null)
    {
        if (!CompanyIds.Contains(companyId)) return false;
        if (unitId is null) return sectorId is null && IsFullCompany(companyId);
        if (UnitCompanies is not null && (!UnitCompanies.TryGetValue(unitId.Value, out var parentCompany) || parentCompany != companyId)) return false;
        if (sectorId.HasValue && SectorUnits is not null && (!SectorUnits.TryGetValue(sectorId.Value, out var parentUnit) || parentUnit != unitId)) return false;
        return UnitIds.Contains(unitId.Value)
            && (FullUnitIds.Contains(unitId.Value) || sectorId.HasValue && SectorIds.Contains(sectorId.Value));
    }
}

public interface IBrowserWorkspaceAccessReader
{
    Task<BrowserWorkspaceAccess?> ReadAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class BrowserWorkspaceAccessReader(IUserAuthenticationRepository authentication, SesmtDbContext db)
    : IBrowserWorkspaceAccessReader
{
    public async Task<BrowserWorkspaceAccess?> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var identity = await authentication.GetByIdAsync(userId, cancellationToken);
        if (identity is null || !identity.User.Active || identity.User.DeletedAt is not null || identity.User.MfaEnabled) return null;
        var ownOnly = identity.Roles.Count > 0 && identity.Roles.All(role =>
            role.Equals("TRABALHADOR", StringComparison.OrdinalIgnoreCase) || role.Equals("WORKER", StringComparison.OrdinalIgnoreCase));
        var workers = await db.Workers.AsNoTracking()
            .Where(worker => worker.UserId == userId && worker.Active && worker.DeletedAt == null)
            .Select(worker => new { worker.Id, worker.CompanyId, worker.UnitId, worker.DepartmentId }).Take(2).ToListAsync(cancellationToken);
        if (ownOnly && workers.Count != 1) return null;

        var assignments = await (from assignment in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.UserId == userId && assignment.Active && assignment.DeletedAt == null && role.Active && role.DeletedAt == null
            select new { assignment.RoleId, assignment.CompanyId, role.Code }).ToListAsync(cancellationToken);
        var nativeGrants = await (from assignment in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join roleGrant in db.RolePermissions.AsNoTracking() on role.Id equals roleGrant.RoleId
            join permission in db.Permissions.AsNoTracking() on roleGrant.PermissionId equals permission.Id
            where assignment.UserId == userId && assignment.Active && assignment.DeletedAt == null && role.Active && role.DeletedAt == null
                && roleGrant.Active && roleGrant.DeletedAt == null && permission.Active && permission.DeletedAt == null
            select new { assignment.CompanyId, permission.Resource, permission.Action }).ToListAsync(cancellationToken);
        var scopes = await db.UserScopes.AsNoTracking().Where(scope => scope.UserId == userId && scope.Active
            && scope.DeletedAt == null && scope.ReferenceId.HasValue)
            .Select(scope => new { scope.ScopeType, ReferenceId = scope.ReferenceId!.Value }).ToListAsync(cancellationToken);
        var companies = await db.Companies.AsNoTracking().Where(company => company.Active && company.DeletedAt == null)
            .Select(company => company.Id).ToListAsync(cancellationToken);
        var units = await db.Units.AsNoTracking().Where(unit => unit.Active && unit.DeletedAt == null && companies.Contains(unit.CompanyId))
            .Select(unit => new { unit.Id, unit.CompanyId }).ToListAsync(cancellationToken);
        var unitIds = units.Select(unit => unit.Id).ToList();
        var sectors = await db.Departments.AsNoTracking().Where(sector => sector.Active && sector.DeletedAt == null && unitIds.Contains(sector.UnitId))
            .Select(sector => new { sector.Id, sector.UnitId }).ToListAsync(cancellationToken);
        // An explicit global ADMIN assignment is different from merely receiving an ADMIN token claim.
        var globalAdmin = !ownOnly && assignments.Any(assignment => assignment.Code == "ADMIN" && assignment.CompanyId is null);
        var fullCompanies = companies.Where(company => globalAdmin || scopes.Any(scope => scope.ScopeType == ScopeType.EMPRESA && scope.ReferenceId == company)).ToHashSet();
        var fullUnits = units.Where(unit => fullCompanies.Contains(unit.CompanyId)
            || scopes.Any(scope => scope.ScopeType == ScopeType.UNIDADE && scope.ReferenceId == unit.Id)).Select(unit => unit.Id).ToHashSet();
        var allowedSectors = sectors.Where(sector => fullUnits.Contains(sector.UnitId)
            || scopes.Any(scope => scope.ScopeType == ScopeType.SETOR && scope.ReferenceId == sector.Id)).Select(sector => sector.Id).ToHashSet();
        var allowedUnits = units.Where(unit => fullUnits.Contains(unit.Id)
            || sectors.Any(sector => sector.UnitId == unit.Id && allowedSectors.Contains(sector.Id))).Select(unit => unit.Id).ToHashSet();
        var allowedCompanies = companies.Where(company => fullCompanies.Contains(company)
            || units.Any(unit => unit.CompanyId == company && allowedUnits.Contains(unit.Id))).ToHashSet();
        if (ownOnly)
        {
            var worker = workers[0];
            var allowedWorker = allowedCompanies.Contains(worker.CompanyId) && worker.UnitId.HasValue && allowedUnits.Contains(worker.UnitId.Value)
                && units.Any(unit => unit.Id == worker.UnitId.Value && unit.CompanyId == worker.CompanyId)
                && (!worker.DepartmentId.HasValue || sectors.Any(sector => sector.Id == worker.DepartmentId.Value && sector.UnitId == worker.UnitId.Value))
                && (fullUnits.Contains(worker.UnitId.Value) || worker.DepartmentId.HasValue && allowedSectors.Contains(worker.DepartmentId.Value));
            allowedCompanies.IntersectWith(allowedWorker ? [worker.CompanyId] : Array.Empty<Guid>());
            fullCompanies.IntersectWith(allowedCompanies);
            allowedUnits.IntersectWith(allowedWorker ? [worker.UnitId!.Value] : Array.Empty<Guid>());
            fullUnits.IntersectWith(allowedUnits);
            allowedSectors.IntersectWith(allowedWorker && worker.DepartmentId.HasValue ? [worker.DepartmentId.Value] : Array.Empty<Guid>());
        }
        var grants = new List<BrowserWorkspaceGrant>();
        foreach (var company in allowedCompanies)
        {
            bool Native(string resource) => nativeGrants.Any(grant => (grant.CompanyId is null || grant.CompanyId == company)
                && grant.Resource == resource && grant.Action == "ler");
            if (!ownOnly && new[] { "empresa", "unidade", "setor", "cargo" }.All(Native)) grants.Add(new("organization.read", company));
            if (Native("trabalhador")) grants.Add(new("workers.read", company));
            if (Native("documento")) grants.Add(new("documents.read", company));
        }
        // A scope reference without any matching resource grant must not become the browser's default unit.
        allowedCompanies.IntersectWith(grants.Select(grant => grant.CompanyId));
        allowedUnits.IntersectWith(units.Where(unit => allowedCompanies.Contains(unit.CompanyId)).Select(unit => unit.Id));
        allowedSectors.IntersectWith(sectors.Where(sector => allowedUnits.Contains(sector.UnitId)).Select(sector => sector.Id));
        fullCompanies.IntersectWith(allowedCompanies);
        fullUnits.IntersectWith(allowedUnits);
        return new(userId, allowedCompanies.Order().ToArray(), allowedUnits.Order().ToArray(), allowedSectors.Order().ToArray(),
            fullCompanies.Order().ToArray(), fullUnits.Order().ToArray(), ownOnly, workers.Count == 1 ? workers[0].Id : null, grants,
            units.Where(unit => allowedUnits.Contains(unit.Id)).ToDictionary(unit => unit.Id, unit => unit.CompanyId),
            sectors.Where(sector => allowedSectors.Contains(sector.Id)).ToDictionary(sector => sector.Id, sector => sector.UnitId));
    }
}
