using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Documents.Interfaces;

public interface IDocumentTypeService
{
    Task<Result<DocumentTypeResponse>> CreateAsync(DocumentTypeCreateRequest request, CancellationToken ct = default);
    Task<Result<DocumentTypeResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IEnumerable<DocumentTypeSummaryResponse>>> ListAsync(string? category, bool? onlyActive, CancellationToken ct = default);
    Task<Result<DocumentTypeResponse>> UpdateAsync(Guid id, DocumentTypeUpdateRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default);
}
