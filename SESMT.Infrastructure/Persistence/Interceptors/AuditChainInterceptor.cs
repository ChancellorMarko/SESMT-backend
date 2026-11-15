using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SESMT.Domain.Modules.Audit.Entities;

namespace SESMT.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Serializes the audit hash chain. Every save that adds audit events takes a transaction-scoped PostgreSQL advisory
/// lock, reads the last stored hash and links the new events to it, in order. Without the lock two requests could read
/// the same last hash and fork the chain. Registered after <see cref="AuditInterceptor"/>, so it also covers the events
/// that interceptor generates. When the caller has no transaction, one is opened here and closed after the save.
/// </summary>
public sealed class AuditChainInterceptor : SaveChangesInterceptor
{
    private const long ChainLockKey = 724891352700;
    private static readonly ConditionalWeakTable<DbContext, IDbContextTransaction> OwnedTransactions = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } db || !UsesPostgres(db)) return result;
        var added = Added(db);
        if (added.Count == 0) return result;
        if (db.Database.CurrentTransaction is null)
            OwnedTransactions.AddOrUpdate(db, await db.Database.BeginTransactionAsync(cancellationToken));
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({ChainLockKey})", cancellationToken);
        Link(added, await LastHash(db).FirstOrDefaultAsync(cancellationToken));
        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not { } db || !UsesPostgres(db)) return result;
        var added = Added(db);
        if (added.Count == 0) return result;
        if (db.Database.CurrentTransaction is null) OwnedTransactions.AddOrUpdate(db, db.Database.BeginTransaction());
        db.Database.ExecuteSqlRaw($"SELECT pg_advisory_xact_lock({ChainLockKey})");
        Link(added, LastHash(db).FirstOrDefault());
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } db && OwnedTransactions.TryGetValue(db, out var transaction))
        {
            OwnedTransactions.Remove(db);
            await using (transaction) await transaction.CommitAsync(cancellationToken);
        }
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } db && OwnedTransactions.TryGetValue(db, out var transaction))
        {
            OwnedTransactions.Remove(db);
            using (transaction) transaction.Commit();
        }
        return result;
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        => await ReleaseAsync(eventData.Context);

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => ReleaseAsync(eventData.Context).GetAwaiter().GetResult();

    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        => await ReleaseAsync(eventData.Context);

    private static async Task ReleaseAsync(DbContext? db)
    {
        if (db is null || !OwnedTransactions.TryGetValue(db, out var transaction)) return;
        OwnedTransactions.Remove(db);
        await using (transaction) await transaction.RollbackAsync();
    }

    private static bool UsesPostgres(DbContext db) => db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

    // Change tracker order follows the order in which the events were added.
    private static List<AuditLog> Added(DbContext db) =>
        db.ChangeTracker.Entries<AuditLog>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToList();

    private static IQueryable<string> LastHash(DbContext db) =>
        db.Set<AuditLog>().AsNoTracking().OrderByDescending(log => log.Id).Select(log => log.CurrentHash);

    private static void Link(List<AuditLog> added, string? lastHash)
    {
        foreach (var log in added)
        {
            log.LinkTo(lastHash);
            lastHash = log.CurrentHash;
        }
    }
}
