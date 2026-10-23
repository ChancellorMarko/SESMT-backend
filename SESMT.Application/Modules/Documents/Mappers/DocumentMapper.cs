using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.Documents.Mappers;

public static class DocumentMapper
{
    public static Document ToEntity(DocumentCreateRequest request) =>
        new(
            documentTypeId: request.DocumentTypeId,
            companyId: request.CompanyId,
            unitId: request.UnitId,
            departmentId: request.DepartmentId,
            code: request.Code,
            title: request.Title,
            description: request.Description,
            secrecyLevel: request.SecrecyLevel,
            issueDate: request.IssueDate,
            validFromDate: request.ValidFromDate,
            validUntilDate: request.ValidUntilDate,
            ownerId: request.OwnerId,
            keywords: request.Keywords,
            metadata: request.Metadata);

    public static DocumentResponse ToResponse(Document document) =>
        new()
        {
            Id = document.Id,
            DocumentTypeId = document.DocumentTypeId,
            CompanyId = document.CompanyId,
            UnitId = document.UnitId,
            DepartmentId = document.DepartmentId,
            OwnerId = document.OwnerId,
            Code = document.Code,
            Title = document.Title,
            Description = document.Description,
            Status = document.Status,
            SecrecyLevel = document.SecrecyLevel,
            IssueDate = document.IssueDate,
            ValidFromDate = document.ValidFromDate,
            ValidUntilDate = document.ValidUntilDate,
            Keywords = document.Keywords,
            Metadata = document.Metadata,
            Active = document.Active && document.DeletedAt is null
        };

    public static DocumentSummaryResponse ToSummary(Document document) =>
        new()
        {
            Id = document.Id,
            Code = document.Code,
            Title = document.Title,
            Status = document.Status,
            SecrecyLevel = document.SecrecyLevel,
            IssueDate = document.IssueDate,
            ValidUntilDate = document.ValidUntilDate,
            Active = document.Active && document.DeletedAt is null
        };
}
