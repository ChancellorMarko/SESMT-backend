using System.Linq.Expressions;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Application.Modules.Documents.Interfaces;

public interface IDocumentAccessPolicy
{
    Task<Expression<Func<Document, bool>>> ReadFilterAsync(Guid actorId, CancellationToken ct);
    Task<bool> CanAccessAsync(Guid actorId, Document document, string action, CancellationToken ct);
    Task<bool> ValidateContextAsync(Guid actorId, Document document, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct);
    Task<bool> CanReviewAsync(Guid actorId, Document document, CancellationToken ct);
    Task<bool> CanGrantAsync(Guid actorId, Document document, CancellationToken ct);
    Task<bool> CanReceiveGrantAsync(Guid userId, Document document, CancellationToken ct);
    Task AddLinksAsync(Guid documentId, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct);
    Task AddGrantAsync(DocumentAccessGrant grant, CancellationToken ct);
    Task<DocumentAccessGrant?> GetGrantAsync(Guid grantId, Guid documentId, CancellationToken ct);
}
