using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Domain.Modules.Organization.Repositories;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByNameAsync(
        Guid unitId,
        string name,
        Guid? ignoreId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Department>> ListByUnitAsync(
        Guid unitId,
        int skip,
        int take,
        CancellationToken ct = default);

    Task AddAsync(Department department, CancellationToken ct = default);

    void Update(Department department);
}
