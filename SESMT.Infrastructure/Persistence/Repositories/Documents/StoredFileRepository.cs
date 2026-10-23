using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public class StoredFileRepository : IStoredFileRepository
{
    private readonly SesmtDbContext _db;
    public StoredFileRepository(SesmtDbContext db) => _db = db;

    public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.StoredFiles.FirstOrDefaultAsync(f => f.Id == id && f.DeletedAt == null, ct);

    public async Task AddAsync(StoredFile file, CancellationToken ct = default)
        => await _db.StoredFiles.AddAsync(file, ct);

    public void Update(StoredFile file) => _db.StoredFiles.Update(file);
}
