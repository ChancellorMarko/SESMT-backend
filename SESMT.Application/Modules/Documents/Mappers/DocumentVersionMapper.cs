using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.Documents.Mappers;

public static class DocumentVersionMapper
{
    public static DocumentVersionResponse ToResponse(DocumentVersion version) =>
        new()
        {
            Id = version.Id,
            DocumentId = version.DocumentId,
            StoredFileId = version.StoredFileId,
            VersionNumber = version.VersionNumber,
            FileHash = version.FileHash,
            Justification = version.Justification,
            State = version.State,
            IsCurrent = version.IsCurrent,
            AuthorId = version.AuthorId,
            ApproverId = version.ApproverId,
            ApprovedAt = version.ApprovedAt,
            ReviewedAt = version.ApprovedAt ?? (version.State == SESMT.Domain.Modules.Documents.Constants.DocumentVersionState.Rejected ? version.UpdatedAt : null),
            CreatedAt = version.CreatedAt
        };
}
