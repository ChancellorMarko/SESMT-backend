using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.Documents.Interfaces;

public interface IDocumentVersionWorkflow
{
    Task ReviewAsync(Document document, DocumentVersion version, Guid actorId, bool accepted, string? reason, CancellationToken ct);
}
