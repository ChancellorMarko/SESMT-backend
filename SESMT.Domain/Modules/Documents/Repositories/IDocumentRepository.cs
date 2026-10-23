using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Domain.Modules.Documents.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByCodeAsync(
        Guid documentTypeId, string code, Guid? unitId, Guid? ignoreId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Document>> ListAsync(
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
        CancellationToken ct = default);

    Task AddAsync(Document document, CancellationToken ct = default);

    void Update(Document document);
}
