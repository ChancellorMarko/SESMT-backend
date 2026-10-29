namespace SESMT.Application.Modules.Documents.DTOs;

public sealed class DocumentSearchRequest
{
    public Guid? CompanyId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? TeamId { get; set; }
    public string? Status { get; set; }
    public string? SecrecyLevel { get; set; }
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? ValidUntilBefore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record DocumentPageResponse(IReadOnlyList<DocumentSummaryResponse> Items, int Page, int PageSize, int Total);
public sealed record DocumentReviewRequest(int ExpectedVersion, int Version, string Decision, string? Reason);
public sealed record DocumentGrantRequest(Guid UserId, bool CanReview, DateTimeOffset ExpiresAt, int ExpectedVersion);
public sealed record DocumentGrantResponse(Guid Id, Guid DocumentId, Guid UserId, bool CanReview, DateTimeOffset ExpiresAt, int Version);
public sealed class DocumentOperationException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class DocumentFileOptions
{
    public const string SectionName = "Documents:Files";
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedMimeTypes { get; set; } = ["application/pdf", "image/png", "image/jpeg"];
}

public sealed class DocumentQueryOptions
{
    public const string SectionName = "Documents:Search";
    public int MaxPageSize { get; set; } = 100;
}
