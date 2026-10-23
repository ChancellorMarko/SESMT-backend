using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public class DocumentVersionRepository : IDocumentVersionRepository
{
    private readonly SesmtDbContext _db;
    public DocumentVersionRepository(SesmtDbContext db) => _db = db;

    public Task<DocumentVersion?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == id && v.DeletedAt == null, ct);

    public Task<DocumentVersion?> GetCurrentByDocumentAsync(Guid documentId, CancellationToken ct = default)
        => _db.DocumentVersions.FirstOrDefaultAsync(v =>
            v.DocumentId == documentId && v.IsCurrent && v.DeletedAt == null, ct);

    public async Task<int> GetNextVersionNumberAsync(Guid documentId, CancellationToken ct = default)
    {
        var max = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .MaxAsync(v => (int?)v.VersionNumber, ct);

        return (max ?? 0) + 1;
    }

    public async Task<IReadOnlyList<DocumentVersion>> ListByDocumentAsync(
        Guid documentId, CancellationToken ct = default)
        => await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.DeletedAt == null)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

    public async Task AddAsync(DocumentVersion version, CancellationToken ct = default)
        => await _db.DocumentVersions.AddAsync(version, ct);

    public void Update(DocumentVersion version) => _db.DocumentVersions.Update(version);
}
