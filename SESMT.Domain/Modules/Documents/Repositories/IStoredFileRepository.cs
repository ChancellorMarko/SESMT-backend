using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Domain.Modules.Documents.Repositories;

public interface IStoredFileRepository
{
    Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(StoredFile file, CancellationToken ct = default);

    void Update(StoredFile file);
}
