using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Workforce.Interfaces;

public interface IWorkerHistoryService
{
    Task<Result<IEnumerable<WorkerHistoryResponse>>> ListByWorkerAsync(
        Guid workerId,
        CancellationToken cancellationToken = default);

    Task<Result<WorkerHistoryResponse>> AddManualAsync(
        Guid workerId,
        WorkerHistoryCreateRequest request,
        CancellationToken cancellationToken = default);
}
