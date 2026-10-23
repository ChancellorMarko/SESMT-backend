using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public class DocumentTypeRepository : IDocumentTypeRepository
{
    private readonly SesmtDbContext _db;
    public DocumentTypeRepository(SesmtDbContext db) => _db = db;

    public Task<DocumentType?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null, ct);

    public Task<DocumentType?> GetByCodeAsync(string code, CancellationToken ct = default)
        => _db.DocumentTypes.FirstOrDefaultAsync(t => t.Code == code && t.DeletedAt == null, ct);

    public Task<bool> ExistsByCodeAsync(string code, Guid? ignoreId = null, CancellationToken ct = default)
        => _db.DocumentTypes.AnyAsync(t =>
            t.Code == code && (ignoreId == null || t.Id != ignoreId) && t.DeletedAt == null, ct);

    public async Task<IReadOnlyList<DocumentType>> ListAsync(
        string? category, bool? onlyActive, CancellationToken ct = default)
    {
        var query = _db.DocumentTypes.Where(t => t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        if (onlyActive == true)
            query = query.Where(t => t.Active);

        return await query.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task AddAsync(DocumentType type, CancellationToken ct = default)
        => await _db.DocumentTypes.AddAsync(type, ct);

    public void Update(DocumentType type) => _db.DocumentTypes.Update(type);
}
