using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Workforce.Interfaces;

public interface IWorkerService
{
    Task<Result<WorkerResponse>> CreateAsync(
        WorkerCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<WorkerResponse>> GetByIdAsync(
        Guid workerId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<WorkerSummaryResponse>>> ListAsync(
        Guid companyId,
        Guid? unitId = null,
        Guid? departmentId = null,
        Guid? positionId = null,
        string? search = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<WorkerResponse>> UpdateAsync(
        Guid workerId,
        WorkerUpdateRequest updateData,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeactivateAsync(
        Guid workerId,
        CancellationToken cancellationToken = default);
}
