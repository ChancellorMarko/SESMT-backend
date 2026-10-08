using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Modules.Documents.Constants;

namespace SESMT.Domain.Modules.Documents.Entities;

public class DocumentLink : AggregateRootBase
{
    public Guid DocumentId { get; private set; }
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }

    protected DocumentLink()
    {
        EntityType = null!;
    }

    public DocumentLink(Guid documentId, string entityType, Guid entityId)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("O documento é obrigatório.");

        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("O tipo da entidade vinculada é obrigatório.");

        if (!LinkedEntityType.IsValid(entityType))
            throw new ArgumentException(
                $"Tipo de entidade vinculada inválido: '{entityType}'. " +
                $"Valores aceitos: {string.Join(", ", LinkedEntityType.All)}.");

        if (entityId == Guid.Empty)
            throw new ArgumentException("O ID da entidade vinculada é obrigatório.");

        DocumentId = documentId;
        EntityType = entityType.Trim().ToUpperInvariant();
        EntityId = entityId;
    }
}