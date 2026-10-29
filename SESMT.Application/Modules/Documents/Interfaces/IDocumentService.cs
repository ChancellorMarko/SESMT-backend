using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Common.Result;

namespace SESMT.Application.Modules.Documents.Interfaces;

public interface IDocumentService
{
    Task<Result<DocumentPageResponse>> SearchAsync(DocumentSearchRequest request, CancellationToken ct = default);
    Task<Result<DocumentVersionResponse>> ReviewAsync(Guid documentId, Guid versionId, DocumentReviewRequest request, CancellationToken ct = default);
    Task<Result<FileDownloadResult>> DownloadFileAsync(Guid documentId, Guid fileId, CancellationToken ct = default);
    Task<Result<DocumentGrantResponse>> GrantAsync(Guid documentId, DocumentGrantRequest request, CancellationToken ct = default);
    Task RevokeGrantAsync(Guid documentId, Guid grantId, int expectedVersion, CancellationToken ct = default);
    Task<Result<DocumentResponse>> CreateAsync(DocumentCreateRequest request, CancellationToken ct = default);

    Task<Result<DocumentResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<IEnumerable<DocumentSummaryResponse>>> ListAsync(
        Guid companyId, Guid? unitId, Guid? departmentId, Guid? documentTypeId,
        string? status, string? secrecyLevel, string? search,
        DateOnly? validUntilBefore, int pageSize, int pageNumber, CancellationToken ct = default);

    Task<Result<DocumentResponse>> UpdateAsync(Guid id, DocumentUpdateRequest request, CancellationToken ct = default);

    Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default);

    // ─── Fluxo de aprovação ──────────────────────────────────────────────
    Task<Result<DocumentResponse>> SubmitForReviewAsync(Guid id, CancellationToken ct = default);
    Task<Result<DocumentResponse>> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<Result<DocumentResponse>> PublishAsync(Guid id, CancellationToken ct = default);

    // ─── Versionamento ───────────────────────────────────────────────────
    Task<Result<DocumentVersionResponse>> AddVersionAsync(
        Guid documentId, DocumentVersionCreateRequest request,
        Stream fileContent, string fileName, string mimeType,
        CancellationToken ct = default);

    Task<Result<IEnumerable<DocumentVersionResponse>>> ListVersionsAsync(Guid documentId, CancellationToken ct = default);

    Task<Result<DocumentVersionResponse>> ApproveVersionAsync(Guid documentId, Guid versionId, DocumentVersionApprovalRequest request, CancellationToken ct = default);
    Task<Result<DocumentVersionResponse>> RejectVersionAsync(Guid documentId, Guid versionId, DocumentVersionRejectionRequest request, CancellationToken ct = default);

    // ─── Download de versão ──────────────────────────────────────────────
    Task<Result<FileDownloadResult>> DownloadVersionAsync(Guid documentId, Guid versionId, CancellationToken ct = default);
}

public sealed record FileDownloadResult(
    Stream Content,
    string FileName,
    string MimeType,
    long SizeBytes,
    string FileHash);
