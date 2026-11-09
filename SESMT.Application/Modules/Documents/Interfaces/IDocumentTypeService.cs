using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Documents.Interfaces;

public interface IDocumentTypeService
{
    Task<Result<DocumentTypeResponse>> CreateAsync(
        Guid actorId, DocumentTypeCreateRequest request, CancellationToken ct = default);

    Task<Result<DocumentTypeResponse>> GetByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<Result<IEnumerable<DocumentTypeSummaryResponse>>> ListAsync(
        string? category, bool? onlyActive, CancellationToken ct = default);

    Task<Result<DocumentTypeResponse>> UpdateAsync(
        Guid actorId, Guid id, DocumentTypeUpdateRequest request, CancellationToken ct = default);

    Task<Result<bool>> DeactivateAsync(Guid actorId, Guid id, CancellationToken ct = default);

    Task<Result<DocumentTypeManagementContextResponse>> GetManagementContextAsync(
        Guid userId, CancellationToken ct = default);

    Task<Result<DocumentTypePageResponse>> ListPagedAsync(
        string? q, int page, int pageSize, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DocumentTypeHistoryEntryResponse>>> GetHistoryAsync(
        Guid id, CancellationToken ct = default);
}
