using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public class DocumentRepository : IDocumentRepository
{
    private readonly SesmtDbContext _db;
    public DocumentRepository(SesmtDbContext db) => _db = db;

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);

    public Task<bool> ExistsByCodeAsync(
        Guid documentTypeId, string code, Guid? unitId, Guid? ignoreId = null,
        CancellationToken ct = default)
        => _db.Documents.AnyAsync(d =>
            d.DocumentTypeId == documentTypeId &&
            d.Code == code &&
            d.UnitId == unitId &&
            (ignoreId == null || d.Id != ignoreId) &&
            d.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Document>> ListAsync(
        Guid companyId,
        Guid? unitId,
        Guid? departmentId,
        Guid? documentTypeId,
        string? status,
        string? secrecyLevel,
        string? search,
        DateOnly? validUntilBefore,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.Documents
            .Where(d => d.CompanyId == companyId && d.DeletedAt == null);

        if (unitId.HasValue) query = query.Where(d => d.UnitId == unitId.Value);
        if (departmentId.HasValue) query = query.Where(d => d.DepartmentId == departmentId.Value);
        if (documentTypeId.HasValue) query = query.Where(d => d.DocumentTypeId == documentTypeId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(d => d.Status == status.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(secrecyLevel)) query = query.Where(d => d.SecrecyLevel == secrecyLevel);
        if (validUntilBefore.HasValue) query = query.Where(d => d.ValidUntilDate != null && d.ValidUntilDate < validUntilBefore.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(d =>
                EF.Functions.ILike(d.Title, term) ||
                EF.Functions.ILike(d.Code, term));
        }

        return await query
            .OrderByDescending(d => d.IssueDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
        => await _db.Documents.AddAsync(document, ct);

    public void Update(Document document) => _db.Documents.Update(document);
}
