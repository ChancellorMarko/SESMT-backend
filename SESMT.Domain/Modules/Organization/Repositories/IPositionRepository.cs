using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Domain.Modules.Organization.Repositories;

public interface IPositionRepository
{
    Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByNameAsync(
        Guid companyId,
        string name,
        Guid? ignoreId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Position>> ListByCompanyAsync(
        Guid companyId,
        int skip,
        int take,
        CancellationToken ct = default);

    Task AddAsync(Position position, CancellationToken ct = default);

    void Update(Position position);
}
