using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Organization.Interfaces;

public interface IPositionService
{
    Task<Result<PositionResponse>> CreateAsync(
        PositionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PositionResponse>> GetByIdAsync(
        Guid positionId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<PositionSummaryResponse>>> ListByCompanyAsync(
        Guid companyId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<PositionResponse>> UpdateAsync(
        Guid positionId,
        PositionUpdateRequest updateData,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeactivateAsync(
        Guid positionId,
        CancellationToken cancellationToken = default);
}
