using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Organization;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly SesmtDbContext _db;

    public DepartmentRepository(SesmtDbContext db) => _db = db;

    public Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Departments.FirstOrDefaultAsync(d =>
            d.Id == id && d.DeletedAt == null, ct);

    public Task<bool> ExistsByNameAsync(
        Guid unitId, string name, Guid? ignoreId = null, CancellationToken ct = default)
    {
        var normalizedName = name.Trim();

        return _db.Departments.AnyAsync(d =>
            d.UnitId == unitId &&
            EF.Functions.ILike(d.Name, normalizedName) &&
            (ignoreId == null || d.Id != ignoreId) &&
            d.DeletedAt == null, ct);
    }

    public async Task<IReadOnlyList<Department>> ListByUnitAsync(
        Guid unitId, int skip, int take, CancellationToken ct = default)
        => await _db.Departments
            .Where(d => d.UnitId == unitId && d.Active && d.DeletedAt == null)
            .OrderBy(d => d.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(Department department, CancellationToken ct = default)
        => await _db.Departments.AddAsync(department, ct);

    public void Update(Department department) => _db.Departments.Update(department);
}
