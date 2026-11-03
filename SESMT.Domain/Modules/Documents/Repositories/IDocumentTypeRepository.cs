using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Domain.Modules.Documents.Repositories;

public interface IDocumentTypeRepository
{
    Task<DocumentType?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<DocumentType?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<bool> ExistsByCodeAsync(string code, Guid? ignoreId = null, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentType>> ListAsync(
        string? category, bool? onlyActive, CancellationToken ct = default);

    Task<(IReadOnlyList<DocumentType> Items, int Total)> ListPagedAsync(
        string? q, int page, int pageSize, CancellationToken ct = default);

    Task<bool> UserCanManageAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(DocumentType type, CancellationToken ct = default);

    void Update(DocumentType type);
}
