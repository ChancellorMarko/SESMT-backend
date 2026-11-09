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
    public int Version { get; private set; } = 1;

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

        Version++;

        MarkAsUpdated();
    }

    /// <summary>Changes only the identification of a type. The document policy (secrecy,
    /// signature, approval, periodicity, retention and metadata schema) is immutable once
    /// created, because existing documents depend on it; a different policy needs a new type.</summary>
    public void Rename(string name, string? description, string category)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe o nome do tipo documental.");
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Informe a categoria do tipo documental.");
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Category = category.Trim();
        Version++;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        Version++;  
        MarkAsDeleted();
        MarkAsUpdated();
    }
}