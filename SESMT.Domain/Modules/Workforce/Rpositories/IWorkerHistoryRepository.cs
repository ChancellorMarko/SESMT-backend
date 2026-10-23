using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Domain.Modules.Workforce.Repositories;

public interface IWorkerHistoryRepository
{
    Task<WorkerHistory?> GetActiveByWorkerAsync(
        Guid workerId, CancellationToken ct = default);

    Task<IReadOnlyList<WorkerHistory>> ListByWorkerAsync(
        Guid workerId, CancellationToken ct = default);

    Task AddAsync(WorkerHistory history, CancellationToken ct = default);

    void Update(WorkerHistory history);
}
