using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Documents.DTOs;

public class DocumentTypeCreateRequest
{
    [Required, StringLength(50)] public string Code { get; set; } = null!;
    [Required, StringLength(150)] public string Name { get; set; } = null!;
    [StringLength(1000)] public string? Description { get; set; }
    [Required, StringLength(30)] public string Category { get; set; } = null!;
    [Required, StringLength(20)] public string SecrecyLevel { get; set; } = null!;
    public bool RequiresSignature { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public int? PeriodicityInDays { get; set; }
    public int? RetentionPeriodInYears { get; set; }
    public string MetadataSchema { get; set; } = "{}";
    public int SchemaVersion { get; set; } = 1;
}

public class DocumentTypeUpdateRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = null!;
    [StringLength(1000)] public string? Description { get; set; }
    [Required, StringLength(30)] public string Category { get; set; } = null!;
    [Required, StringLength(20)] public string SecrecyLevel { get; set; } = null!;
    public bool RequiresSignature { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public int? PeriodicityInDays { get; set; }
    public int? RetentionPeriodInYears { get; set; }
    public string MetadataSchema { get; set; } = "{}";
    public int SchemaVersion { get; set; } = 1;
}

public class DocumentTypeResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Category { get; set; } = null!;
    public string SecrecyLevel { get; set; } = null!;
    public bool RequiresSignature { get; set; }
    public bool RequiresApproval { get; set; }
    public int? PeriodicityInDays { get; set; }
    public int? RetentionPeriodInYears { get; set; }
    public string MetadataSchema { get; set; } = null!;
    public int SchemaVersion { get; set; }
    public bool Active { get; set; }
}

public class DocumentTypeSummaryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;
    public bool Active { get; set; }
}
