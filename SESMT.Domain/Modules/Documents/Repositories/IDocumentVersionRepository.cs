using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Domain.Modules.Documents.Repositories;

public interface IDocumentVersionRepository
{
    Task<DocumentVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<DocumentVersion?> GetCurrentByDocumentAsync(Guid documentId, CancellationToken ct = default);

    Task<int> GetNextVersionNumberAsync(Guid documentId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentVersion>> ListByDocumentAsync(Guid documentId, CancellationToken ct = default);

    Task AddAsync(DocumentVersion version, CancellationToken ct = default);

    void Update(DocumentVersion version);
}
