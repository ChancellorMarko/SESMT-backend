namespace SESMT.API.Contracts.Browser;

public sealed record BrowserDocumentPageResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);

public sealed class BrowserDocumentQuery
{
    public Guid? UnitId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Q { get; set; }
    public string? Status { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? WorkerId { get; set; }
    public Guid? ResponsibleId { get; set; }
    public DateOnly? ValidUntilFrom { get; set; }
    public DateOnly? ValidUntilTo { get; set; }
    public DateOnly? IssueDateFrom { get; set; }
    public DateOnly? IssueDateTo { get; set; }
}

public sealed record BrowserDocumentResponse(
    Guid Id, string Code, string Title, Guid TypeId, Guid CompanyId, Guid? UnitId,
    Guid? SectorId, Guid OwnerId, string AccessClass, DateOnly IssueDate,
    DateOnly? ValidFrom, DateOnly? ValidUntil, IReadOnlyCollection<Guid> WorkerIds,
    IReadOnlyCollection<Guid> ResponsibleIds, IReadOnlyCollection<string> Keywords,
    string Status, Guid? CurrentVersionId, Guid? ActiveVersionId,
    IReadOnlyCollection<Guid> ActiveVersionIds, IReadOnlyCollection<string> AvailableActions,
    int? Revision)
{
    public bool SourceReadOnly => true;
}

public sealed record BrowserDocumentFileResponse(
    Guid Id, string Name, string MimeType, long SizeBytes, string Sha256, string ScanStatus);

public sealed record BrowserDocumentApprovalResponse(Guid? ActorId, DateTimeOffset? ApprovedAt);

public sealed record BrowserDocumentVersionResponse(
    Guid Id, Guid DocumentId, int VersionNumber, string? Reason, string Status,
    object? MetadataSnapshot, BrowserDocumentFileResponse? File, DateTime CreatedAt, Guid CreatedBy,
    BrowserDocumentApprovalResponse? Approval, IReadOnlyCollection<object> SignatureEvidences,
    IReadOnlyCollection<string> AvailableActions, int? Revision)
{
    public bool SourceReadOnly => true;
}

public sealed record BrowserDocumentActorResponse(Guid? Id, string? DisplayName);
public sealed record BrowserDocumentHistoryResponse(
    string Id, Guid DocumentId, Guid? VersionId, string Action,
    BrowserDocumentActorResponse Actor, DateTimeOffset OccurredAt);

// The native catalogue has no upload/renewal/workflow policy matching the browser write contract.
// Expose its actual facts separately and leave unsupported policies explicitly unavailable.
public sealed record BrowserDocumentTypeResponse(
    Guid Id, string Code, string Name, string Category, string DefaultAccessClass,
    bool RequiresApproval, bool RequiresSignature, int? PeriodicityInDays,
    int? RetentionPeriodInYears, int SchemaVersion)
{
    public bool SourceReadOnly => true;
    public object? RequiredFields => null;
    public object? AllowedAccessClasses => null;
    public object? AllowedMimeTypes => null;
    public long? MaxFileSizeBytes => null;
    public object? RenewalPolicy => null;
    public object? RetentionPolicyReference => null;
    public object? Workflow => null;
    public int? Revision => null;
}
