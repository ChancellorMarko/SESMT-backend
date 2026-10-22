using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Organization.Interfaces;

public interface IDepartmentService
{
    Task<Result<DepartmentResponse>> CreateAsync(
        DepartmentCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DepartmentResponse>> GetByIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<DepartmentSummaryResponse>>> ListByUnitAsync(
        Guid unitId,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<DepartmentResponse>> UpdateAsync(
        Guid departmentId,
        DepartmentUpdateRequest updateData,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeactivateAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default);
}
