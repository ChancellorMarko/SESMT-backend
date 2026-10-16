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
            w.Id == id && w.Active && w.DeletedAt == null, ct);

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
}
