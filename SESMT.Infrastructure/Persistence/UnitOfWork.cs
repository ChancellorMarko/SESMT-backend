using SESMT.Application.Common;

namespace SESMT.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly SesmtDbContext _db;
    public UnitOfWork(SesmtDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}