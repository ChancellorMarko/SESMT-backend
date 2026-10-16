using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Organization.Interfaces;

public interface ICompanyService
{
    Task<Result<CompanyResponse>> CreateAsync(
        CompanyCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyResponse>> GetByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<CompanySummaryResponse>>> ListAsync(
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyResponse>> UpdateAsync(
        Guid companyId,
        CompanyCreateRequest updateData,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeactivateAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);
}