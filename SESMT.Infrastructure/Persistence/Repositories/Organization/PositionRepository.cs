using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Organization;

public class PositionRepository : IPositionRepository
{
    private readonly SesmtDbContext _db;

    public PositionRepository(SesmtDbContext db) => _db = db;

    public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Positions.FirstOrDefaultAsync(p =>
            p.Id == id && p.DeletedAt == null, ct);

    public Task<bool> ExistsByNameAsync(
        Guid companyId, string name, Guid? ignoreId = null, CancellationToken ct = default)
    {
        var normalizedName = name.Trim();

        return _db.Positions.AnyAsync(p =>
            p.CompanyId == companyId &&
            EF.Functions.ILike(p.Name, normalizedName) &&
            (ignoreId == null || p.Id != ignoreId) &&
            p.DeletedAt == null, ct);
    }

    public async Task<IReadOnlyList<Position>> ListByCompanyAsync(
        Guid companyId, int skip, int take, CancellationToken ct = default)
        => await _db.Positions
            .Where(p => p.CompanyId == companyId && p.Active && p.DeletedAt == null)
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(Position position, CancellationToken ct = default)
        => await _db.Positions.AddAsync(position, ct);

    public void Update(Position position) => _db.Positions.Update(position);
}
