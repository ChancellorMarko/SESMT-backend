using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Documents.DTOs;

public class DocumentCreateRequest
{
    [Required] public Guid DocumentTypeId { get; set; }
    [Required] public Guid CompanyId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    [Required, StringLength(80)] public string Code { get; set; } = null!;
    [Required, StringLength(255)] public string Title { get; set; } = null!;
    [StringLength(2000)] public string? Description { get; set; }
    [Required, StringLength(20)] public string SecrecyLevel { get; set; } = null!;
    [Required] public DateOnly IssueDate { get; set; }
    public DateOnly? ValidFromDate { get; set; }
    public DateOnly? ValidUntilDate { get; set; }
    [Required] public Guid OwnerId { get; set; }
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Metadata { get; set; } = "{}";

    // ─── Arquivo inicial (opcional — cria a versão 1) ──────────────────────
    public IReadOnlyCollection<DocumentLinkCreateRequest> Links { get; set; }
        = Array.Empty<DocumentLinkCreateRequest>();
}

public class DocumentLinkCreateRequest
{
    [Required] public string EntityType { get; set; } = null!;
    [Required] public Guid EntityId { get; set; }
}

public class DocumentUpdateRequest
{
    [Required, StringLength(255)] public string Title { get; set; } = null!;
    [StringLength(2000)] public string? Description { get; set; }
    [Required, StringLength(20)] public string SecrecyLevel { get; set; } = null!;
    [Required] public DateOnly IssueDate { get; set; }
    public DateOnly? ValidFromDate { get; set; }
    public DateOnly? ValidUntilDate { get; set; }
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Metadata { get; set; } = "{}";
}

public class DocumentResponse
{
    public Guid Id { get; set; }
    public Guid DocumentTypeId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid OwnerId { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public string SecrecyLevel { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public DateOnly? ValidFromDate { get; set; }
    public DateOnly? ValidUntilDate { get; set; }
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Metadata { get; set; } = null!;
    public bool Active { get; set; }
}

public class DocumentSummaryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string SecrecyLevel { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public DateOnly? ValidUntilDate { get; set; }
    public bool Active { get; set; }
}
