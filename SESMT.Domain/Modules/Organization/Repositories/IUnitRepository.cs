using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Domain.Modules.Organization.Repositories;

public interface IUnitRepository
{
    Task<Unit?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByCodeAsync(
        Guid companyId,
        string code,
        CancellationToken ct = default);

    Task<IReadOnlyList<Unit>> ListByCompanyAsync(
        Guid companyId,
        int skip,
        int take,
        CancellationToken ct = default);

    Task<IReadOnlyList<Unit>> ListAsync(
        Guid? companyId,
        string? code,
        int skip,
        int take,
        CancellationToken ct = default);

    Task AddAsync(Unit unit, CancellationToken ct = default);

    void Update(Unit unit);
}