namespace SESMT.Application.Modules.Documents.DTOs;

public sealed record DocumentTypeManagementContextResponse(bool CanManage);

public sealed record DocumentTypeDetailResponse(DocumentTypeResponse Item, int Version);

public sealed record DocumentTypeListItemResponse(
    Guid Id,
    DocumentTypeResponse Item,
    int Version);

public sealed record DocumentTypePageResponse(
    IReadOnlyList<DocumentTypeListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DocumentTypeHistoryEntryResponse(
    Guid Id,
    string Action,
    string? ActorName,
    DateTimeOffset OccurredAt,
    int Version);
