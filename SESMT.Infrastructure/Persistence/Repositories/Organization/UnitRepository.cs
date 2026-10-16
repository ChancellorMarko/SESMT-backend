using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Organization;

public class UnitRepository : IUnitRepository
{
    private readonly SesmtDbContext _db;

    public UnitRepository(SesmtDbContext db) => _db = db;

    public Task<Unit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Units.FirstOrDefaultAsync(u =>
            u.Id == id && u.DeletedAt == null, ct);

    public Task<bool> ExistsByCodeAsync(
        Guid companyId, string code, CancellationToken ct = default)
        => _db.Units.AnyAsync(u =>
            u.CompanyId == companyId &&
            EF.Functions.ILike(u.Code, code.Trim()) &&
            u.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Unit>> ListByCompanyAsync(
        Guid companyId, int skip, int take, CancellationToken ct = default)
        => await _db.Units
            .Where(u => u.CompanyId == companyId
                        && u.Active
                        && u.DeletedAt == null)
            .OrderBy(u => u.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Unit>> ListAsync(
        Guid? companyId, string? code, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Units
            .Where(u => u.Active && u.DeletedAt == null);

        if (companyId.HasValue)
            query = query.Where(u => u.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(u => EF.Functions.ILike(u.Code, $"%{code.Trim()}%"));

        return await query
            .OrderBy(u => u.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Unit unit, CancellationToken ct = default)
        => await _db.Units.AddAsync(unit, ct);

    public void Update(Unit unit) => _db.Units.Update(unit);
}
