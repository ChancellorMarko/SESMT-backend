using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Organization.Interfaces;

public interface ITeamService
{
    Task<Result<TeamResponse>> CreateAsync(
        TeamCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TeamResponse>> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<TeamSummaryResponse>>> ListByLocationAsync(
        Guid locationId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<TeamResponse>> UpdateAsync(
        Guid teamId,
        TeamPatchRequest patchData,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeactivateAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);
}