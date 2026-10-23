using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Domain.Modules.Workforce.Repositories;

public interface IWorkerRepository
{
    Task<Worker?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Worker>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<bool> ExistsInCompanyAsync(
        Guid workerId, Guid companyId, CancellationToken ct = default);

    Task<bool> ExistsByEnrollmentAsync(
        Guid companyId, string enrollment, Guid? ignoreId = null,
        CancellationToken ct = default);

    Task<bool> ExistsByCpfHashAsync(
        Guid companyId, string cpfHash, Guid? ignoreId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Worker>> ListByCompanyAsync(
        Guid companyId,
        Guid? unitId,
        Guid? departmentId,
        Guid? positionId,
        string? search,
        int skip,
        int take,
        CancellationToken ct = default);

    Task AddAsync(Worker worker, CancellationToken ct = default);

    void Update(Worker worker);
}
