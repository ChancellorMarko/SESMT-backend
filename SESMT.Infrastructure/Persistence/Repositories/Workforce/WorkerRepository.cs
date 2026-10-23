using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.Workforce.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Workforce;

public class WorkerRepository : IWorkerRepository
{
    private readonly SesmtDbContext _db;

    public WorkerRepository(SesmtDbContext db) => _db = db;

    public Task<Worker?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Workers.FirstOrDefaultAsync(w =>
            w.Id == id && w.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Worker>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<Worker>();

        return await _db.Workers
            .Where(w => idList.Contains(w.Id) && w.Active && w.DeletedAt == null)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsInCompanyAsync(
        Guid workerId, Guid companyId, CancellationToken ct = default)
        => _db.Workers.AnyAsync(w =>
            w.Id == workerId
            && w.CompanyId == companyId
            && w.Active
            && w.DeletedAt == null, ct);

    public Task<bool> ExistsByEnrollmentAsync(
        Guid companyId, string enrollment, Guid? ignoreId = null,
        CancellationToken ct = default)
    {
        var normalized = enrollment.Trim();

        return _db.Workers.AnyAsync(w =>
            w.CompanyId == companyId &&
            EF.Functions.ILike(w.Enrollment, normalized) &&
            (ignoreId == null || w.Id != ignoreId) &&
            w.DeletedAt == null, ct);
    }

    public Task<bool> ExistsByCpfHashAsync(
        Guid companyId, string cpfHash, Guid? ignoreId = null,
        CancellationToken ct = default)
        => _db.Workers.AnyAsync(w =>
            w.CompanyId == companyId &&
            w.CpfHash == cpfHash &&
            (ignoreId == null || w.Id != ignoreId) &&
            w.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Worker>> ListByCompanyAsync(
    Guid companyId,
    Guid? unitId,
    Guid? departmentId,
    Guid? positionId,
    string? search,
    int skip,
    int take,
    CancellationToken ct = default)
    {
        var query = _db.Workers
            .Where(w => w.CompanyId == companyId && w.DeletedAt == null);

        if (unitId.HasValue)
            query = query.Where(w => w.UnitId == unitId.Value);

        if (departmentId.HasValue)
            query = query.Where(w => w.DepartmentId == departmentId.Value);

        if (positionId.HasValue)
            query = query.Where(w => w.PositionId == positionId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(w =>
                EF.Functions.ILike(w.Name.Value, term) ||
                EF.Functions.ILike(w.Enrollment, term));
        }

        return await query
            .OrderBy(w => w.Name.Value)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Worker worker, CancellationToken ct = default)
        => await _db.Workers.AddAsync(worker, ct);

    public void Update(Worker worker) => _db.Workers.Update(worker);
}
