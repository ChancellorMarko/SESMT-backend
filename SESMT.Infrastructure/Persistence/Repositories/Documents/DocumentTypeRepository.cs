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

    public async Task<(IReadOnlyList<DocumentType> Items, int Total)> ListPagedAsync(
    string? q, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.DocumentTypes
            .AsNoTracking()
            .Where(t => t.Active && t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.Code.ToLower().Contains(term) ||
                t.Category.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Code).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> UserCanManageAsync(Guid userId, CancellationToken ct = default) =>
        (from ur in _db.UserRoles
         join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
         join p in _db.Permissions on rp.PermissionId equals p.Id
         where ur.UserId == userId
             && ur.Active && ur.DeletedAt == null
             && rp.Active && rp.DeletedAt == null
             && p.Active && p.DeletedAt == null
             && (p.Resource == "parametro" && p.Action == "editar"
                 || p.Resource == "documento" && p.Action == "editar")
         select ur.Id).AnyAsync(ct);

    public async Task AddAsync(DocumentType type, CancellationToken ct = default)
        => await _db.DocumentTypes.AddAsync(type, ct);

    public void Update(DocumentType type) => _db.DocumentTypes.Update(type);
}
