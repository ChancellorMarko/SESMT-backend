using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Repositories.WorkPermits;

public sealed class OperationalDesignationRepository(SesmtDbContext db) : IOperationalDesignationRepository
{
    public async Task<IReadOnlyList<Guid>> GetScopedCompanyIdsAsync(Guid actorId, CancellationToken ct = default) =>
        await (from scope in db.UserScopes join company in db.Companies on scope.ReferenceId equals company.Id
            join user in db.Users on scope.UserId equals user.Id
            where scope.UserId == actorId && scope.ScopeType == ScopeType.EMPRESA && scope.Active && scope.DeletedAt == null
                && user.Active && user.DeletedAt == null && company.Active && company.DeletedAt == null
            select company.Id).Distinct().ToListAsync(ct);

    public Task<bool> HasRoleInCompanyAsync(Guid actorId, Guid companyId, string roleCode, CancellationToken ct = default) =>
        (from link in db.UserRoles join role in db.Roles on link.RoleId equals role.Id
         where link.UserId == actorId && link.CompanyId == companyId && link.Active && link.DeletedAt == null
            && role.Code == roleCode && role.Active && role.DeletedAt == null
            && db.Users.Any(user => user.Id == actorId && user.Active && user.DeletedAt == null)
         select link.Id).AnyAsync(ct);

    public Task<Guid?> GetActiveWorkerCompanyIdAsync(Guid personId, IReadOnlyCollection<Guid> companies, CancellationToken ct = default) =>
        db.Workers.Where(x => x.Id == personId && companies.Contains(x.CompanyId) && x.Active && x.DeletedAt == null && x.DismissalDate == null)
            .Select(x => (Guid?)x.CompanyId).FirstOrDefaultAsync(ct);

    public async Task<bool> AreActiveLocationsInCompanyAsync(IReadOnlyCollection<Guid> ids, Guid companyId, CancellationToken ct = default) =>
        await db.Units.CountAsync(x => ids.Contains(x.Id) && x.CompanyId == companyId && x.Active && x.DeletedAt == null, ct) == ids.Count;

    public Task AddAsync(OperationalDesignation designation, CancellationToken ct = default) => db.OperationalDesignations.AddAsync(designation, ct).AsTask();
    public Task<OperationalDesignation?> FindDuplicateAsync(OperationalDesignation item, CancellationToken ct = default) =>
        db.OperationalDesignations.FirstOrDefaultAsync(x => x.CompanyId == item.CompanyId && x.PersonId == item.PersonId
            && x.Type == item.Type && x.StartsAt == item.StartsAt && x.EndsAt == item.EndsAt && x.Origin == item.Origin
            && x.ServiceIds.SequenceEqual(item.ServiceIds) && x.LocationIds.SequenceEqual(item.LocationIds), ct);
    public Task<OperationalDesignation?> GetAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct = default) =>
        db.OperationalDesignations.FirstOrDefaultAsync(x => x.Id == id && companies.Contains(x.CompanyId) && x.Active && x.DeletedAt == null, ct);
    public async Task<IReadOnlyList<OperationalDesignation>> ListAsync(IReadOnlyCollection<Guid> companies, CancellationToken ct = default) =>
        await db.OperationalDesignations.Where(x => companies.Contains(x.CompanyId) && x.Active && x.DeletedAt == null)
            .OrderByDescending(x => x.StartsAt).ToListAsync(ct);

    public Task<bool> HasEffectiveDesignationAsync(Guid actorId, Guid companyId, Guid locationId, IReadOnlyCollection<Guid> serviceIds, string type, DateTimeOffset instant, CancellationToken ct = default) =>
        (from designation in db.OperationalDesignations
         join worker in db.Workers on designation.PersonId equals worker.Id
         where designation.CompanyId == companyId && worker.UserId == actorId && worker.Active && worker.DeletedAt == null
            && designation.Type == type && designation.Active && designation.DeletedAt == null
            && designation.RevokedAt == null && designation.StartsAt <= instant && instant < designation.EndsAt
            && designation.LocationIds.Contains(locationId)
            && serviceIds.All(serviceId => designation.ServiceIds.Contains(serviceId))
         select designation.Id).AnyAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await db.SaveChangesAsync(ct);
}
