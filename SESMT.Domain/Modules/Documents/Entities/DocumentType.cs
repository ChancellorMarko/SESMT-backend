using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Documents.Entities;

public class DocumentType : AggregateRootBase
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Category { get; private set; } = null!;
    public string SecrecyLevel { get; private set; } = null!;
    public bool RequiresSignature { get; private set; } = false;
    public bool RequiresApproval { get; private set; } = false;
    public int? PeriodicityInDays { get; private set; }
    public int? RetentionPeriodInYears { get; private set; }
    public string MetadataSchema { get; private set; } = null!;
    public int SchemaVersion { get; private set; } = 1;

    protected DocumentType() { }

    public DocumentType(string code, string name, string? description, string category, string secrecyLevel, bool requiresSignature, bool requiresApproval, int? periodicityInDays, int? retentionPeriodInYears, string metadataSchema, int schemaVersion)
    {
        Code = code;
        Name = name;
        Description = description;
        Category = category;
        SecrecyLevel = secrecyLevel;
        RequiresSignature = requiresSignature;
        RequiresApproval = requiresApproval;
        PeriodicityInDays = periodicityInDays;
        RetentionPeriodInYears = retentionPeriodInYears;
        MetadataSchema = metadataSchema;
        SchemaVersion = schemaVersion;
    }

    public void UpdateDocumentType(string code, string name, string? description, string category, string secrecyLevel, bool requiresSignature, bool requiresApproval, int? periodicityInDays, int? retentionPeriodInYears, string metadataSchema, int schemaVersion)
    {
        Code = code;
        Name = name;
        Description = description;
        Category = category;
        SecrecyLevel = secrecyLevel;
        RequiresSignature = requiresSignature;
        RequiresApproval = requiresApproval;
        PeriodicityInDays = periodicityInDays;
        RetentionPeriodInYears = retentionPeriodInYears;
        MetadataSchema = metadataSchema;
        SchemaVersion = schemaVersion;

        MarkAsUpdated();
    }
}