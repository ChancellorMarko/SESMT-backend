using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;

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
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = BuildQuery(actorId, action, resourceType, resourceId, from, to);

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
        CancellationToken ct = default)
        => BuildQuery(actorId, action, resourceType, resourceId, from, to)
            .CountAsync(ct);

    private IQueryable<AuditLog> BuildQuery(
        Guid? actorId,
        string? action,
        string? resourceType,
        Guid? resourceId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var query = _db.AuditLogs.AsQueryable();

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
