using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.Documents.Mappers;

public static class DocumentTypeMapper
{
    public static DocumentType ToEntity(DocumentTypeCreateRequest request) =>
        new(
            request.Code,
            request.Name,
            request.Description,
            request.Category,
            request.SecrecyLevel,
            request.RequiresSignature,
            request.RequiresApproval,
            request.PeriodicityInDays,
            request.RetentionPeriodInYears,
            request.MetadataSchema,
            request.SchemaVersion);

    public static DocumentTypeResponse ToResponse(DocumentType type) =>
        new()
        {
            Id = type.Id,
            Code = type.Code,
            Name = type.Name,
            Description = type.Description,
            Category = type.Category,
            SecrecyLevel = type.SecrecyLevel,
            RequiresSignature = type.RequiresSignature,
            RequiresApproval = type.RequiresApproval,
            PeriodicityInDays = type.PeriodicityInDays,
            RetentionPeriodInYears = type.RetentionPeriodInYears,
            MetadataSchema = type.MetadataSchema,
            SchemaVersion = type.SchemaVersion,
            Active = type.Active && type.DeletedAt is null
        };

    public static DocumentTypeSummaryResponse ToSummary(DocumentType type) =>
        new()
        {
            Id = type.Id,
            Code = type.Code,
            Name = type.Name,
            Category = type.Category,
            Active = type.Active && type.DeletedAt is null
        };
}
