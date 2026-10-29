using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Documents.DTOs;

public class DocumentVersionCreateRequest
{
    public int? ExpectedVersion { get; set; }
    [Required] public Guid AuthorId { get; set; }
    [StringLength(2000)] public string? Justification { get; set; }
}

public class DocumentVersionResponse
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid StoredFileId { get; set; }
    public int VersionNumber { get; set; }
    public string FileHash { get; set; } = null!;
    public string? Justification { get; set; }
    public string State { get; set; } = null!;
    public bool IsCurrent { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ApproverId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DocumentVersionApprovalRequest
{
    public int ExpectedVersion { get; set; }
    [Required] public Guid ApproverId { get; set; }
}

public class DocumentVersionRejectionRequest
{
    public int ExpectedVersion { get; set; }
    [Required] public Guid ApproverId { get; set; }
    [Required, StringLength(2000)] public string Justification { get; set; } = null!;
}
