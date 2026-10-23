using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.Workforce.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Workforce;

public class WorkerHistoryRepository : IWorkerHistoryRepository
{
    private readonly SesmtDbContext _db;

    public WorkerHistoryRepository(SesmtDbContext db) => _db = db;

    public Task<WorkerHistory?> GetActiveByWorkerAsync(
        Guid workerId, CancellationToken ct = default)
        => _db.WorkerHistories
            .Where(h => h.WorkerId == workerId
                        && h.EndDate == null
                        && h.Active
                        && h.DeletedAt == null)
            .OrderByDescending(h => h.StartDate)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WorkerHistory>> ListByWorkerAsync(
        Guid workerId, CancellationToken ct = default)
        => await _db.WorkerHistories
            .Where(h => h.WorkerId == workerId && h.DeletedAt == null)
            .OrderByDescending(h => h.StartDate)
            .ToListAsync(ct);

    public async Task AddAsync(WorkerHistory history, CancellationToken ct = default)
        => await _db.WorkerHistories.AddAsync(history, ct);

    public void Update(WorkerHistory history) => _db.WorkerHistories.Update(history);
}
