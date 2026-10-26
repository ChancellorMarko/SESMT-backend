using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.Interfaces;

public interface IOperationalDesignationRepository
{
    Task<IReadOnlyList<Guid>> GetScopedCompanyIdsAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> HasRoleInCompanyAsync(Guid actorId, Guid companyId, string roleCode, CancellationToken cancellationToken = default);
    Task<Guid?> GetActiveWorkerCompanyIdAsync(Guid personId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<bool> AreActiveLocationsInCompanyAsync(IReadOnlyCollection<Guid> locationIds, Guid companyId, CancellationToken cancellationToken = default);
    Task AddAsync(OperationalDesignation designation, CancellationToken cancellationToken = default);
    Task<OperationalDesignation?> FindDuplicateAsync(OperationalDesignation designation, CancellationToken cancellationToken = default);
    Task<OperationalDesignation?> GetAsync(Guid id, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationalDesignation>> ListAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken = default);
    Task<bool> HasEffectiveDesignationAsync(Guid actorId, Guid companyId, Guid locationId, IReadOnlyCollection<Guid> serviceIds, string type, DateTimeOffset instant, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
