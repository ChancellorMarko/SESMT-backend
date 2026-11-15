using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.IdentityAccess.Constants;

namespace SESMT.Infrastructure.Persistence.Repositories.Audit;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly SesmtDbContext _db;

    public AuditLogRepository(SesmtDbContext db) => _db = db;

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
        => await _db.AuditLogs.AddAsync(log, ct);

    public Task<string?> GetLastHashAsync(CancellationToken ct = default)
        => _db.AuditLogs
            .OrderByDescending(a => a.Id)
            .Select(a => a.CurrentHash)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid? actorId,
        string? action,
        string? resourceType,
        Guid? resourceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid> companyIds,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = BuildQuery(actorId, action, resourceType, resourceId, from, to, companyIds);

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Guid? actorId,
        string? action,
        string? resourceType,
        Guid? resourceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken ct = default)
        => BuildQuery(actorId, action, resourceType, resourceId, from, to, companyIds)
            .CountAsync(ct);

    public async Task<IReadOnlyCollection<Guid>> GetAccessibleCompanyIdsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserScopes
            .Where(scope => scope.UserId == userId
                && scope.ScopeType == ScopeType.EMPRESA
                && scope.ReferenceId.HasValue
                && scope.Active && scope.DeletedAt == null
                && _db.Users.Any(user => user.Id == userId && user.Active && user.DeletedAt == null)
                && _db.Companies.Any(company => company.Id == scope.ReferenceId.Value && company.Active && company.DeletedAt == null))
            .Select(scope => scope.ReferenceId!.Value)
            .Distinct()
            .ToListAsync(ct);

    public Task<AuditLog?> GetByEventIdAsync(Guid eventId, IReadOnlyCollection<Guid> companyIds, CancellationToken ct = default) =>
        BuildQuery(null, null, null, null, null, null, companyIds)
            .FirstOrDefaultAsync(log => log.EventId == eventId, ct);

    private IQueryable<AuditLog> BuildQuery(
        Guid? actorId,
        string? action,
        string? resourceType,
        Guid? resourceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IReadOnlyCollection<Guid> companyIds)
    {
        var query = _db.AuditLogs.AsQueryable();

        query = companyIds.Count == 0
            ? query.Where(log => log.CompanyId == null)
            : query.Where(log => log.CompanyId.HasValue && companyIds.Contains(log.CompanyId.Value));

        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(a => a.ResourceType == resourceType.ToLowerInvariant());

        if (resourceId.HasValue)
            query = query.Where(a => a.ResourceId == resourceId.Value);

        if (from.HasValue)
            query = query.Where(a => a.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredAt <= to.Value);

        return query;
    }
}

public sealed class AuditChainVerifier(SesmtDbContext db) : IAuditChainVerifier
{
    private readonly SesmtDbContext _db = db;

    public async Task<AuditChainReport> VerifyAsync(CancellationToken ct = default)
    {
        long checkedRows = 0, invalid = 0, forks = 0, genesis = 0;
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var successors = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var predecessors = new List<(long Id, string Hash)>();
        var sample = new List<long>();
        void Flag(long id) { if (sample.Count < 20) sample.Add(id); }
        await foreach (var log in _db.AuditLogs.AsNoTracking().OrderBy(a => a.Id).AsAsyncEnumerable().WithCancellation(ct))
        {
            checkedRows++;
            hashes.Add(log.CurrentHash);
            if (log.VerifyIntegrity() == AuditIntegrity.Invalid) { invalid++; Flag(log.Id); }
            if (string.IsNullOrEmpty(log.PreviousHash)) { genesis++; continue; }
            if (successors.TryAdd(log.PreviousHash, log.Id)) predecessors.Add((log.Id, log.PreviousHash));
            else { forks++; Flag(log.Id); }
        }
        var missing = predecessors.Where(link => !hashes.Contains(link.Hash)).ToList();
        foreach (var link in missing) Flag(link.Id);
        return new(checkedRows, invalid, forks, missing.Count, genesis, sample);
    }
}
