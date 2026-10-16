using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Organization.Interfaces;

public interface IUnitService
{
    Task<Result<UnitResponse>> CreateAsync(
        UnitCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UnitResponse>> GetByIdAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<UnitSummaryResponse>>> ListByCompanyAsync(
        Guid companyId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<UnitSummaryResponse>>> ListAsync(
        Guid? companyId = null,
        string? code = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<UnitResponse>> UpdateAsync(
        Guid unitId,
        UnitUpdateRequest updateData,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeactivateAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);
}