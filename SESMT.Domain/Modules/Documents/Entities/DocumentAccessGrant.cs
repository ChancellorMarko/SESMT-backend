using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Documents.Entities;

/// <summary>An explicit, expiring grant for a restricted document; it never replaces scope or designation checks.</summary>
public sealed class DocumentAccessGrant : AggregateRootBase
{
    public Guid DocumentId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GrantedBy { get; private set; }
    public bool CanReview { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public int Version { get; private set; } = 1;

    private DocumentAccessGrant() { }
    public DocumentAccessGrant(Guid documentId, Guid userId, Guid grantedBy, bool canReview, DateTimeOffset expiresAt)
    {
        if (documentId == Guid.Empty || userId == Guid.Empty || grantedBy == Guid.Empty)
            throw new ArgumentException("Documento, destinatário e concedente são obrigatórios.");
        if (expiresAt.Offset != TimeSpan.Zero || expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A concessão deve ter validade futura em UTC.");
        DocumentId = documentId; UserId = userId; GrantedBy = grantedBy; CanReview = canReview; ExpiresAt = expiresAt; CreatedBy = grantedBy;
    }
    public void Revoke(Guid actorId, int expectedVersion)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Ator inválido.");
        if (expectedVersion != Version || RevokedAt.HasValue) throw new InvalidOperationException("A concessão foi alterada. Recarregue a versão atual.");
        RevokedAt = DateTimeOffset.UtcNow; Version++; MarkAsUpdated(actorId);
    }
}
