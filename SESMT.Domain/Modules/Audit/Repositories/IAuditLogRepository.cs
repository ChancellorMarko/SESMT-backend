using SESMT.Domain.Modules.Audit.Entities;

namespace SESMT.Domain.Modules.Audit.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);

    /// <summary>
    /// Recupera o hash do último log inserido, para encadear o próximo.
    /// </summary>
    Task<string?> GetLastHashAsync(CancellationToken ct = default);

    /// <summary>
    /// Consulta paginada com filtros — usada pelo endpoint GET /audit-logs.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid? actorId,
        string? action,
        string? resourceType,
        Guid? resourceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken ct = default);

    Task<int> CountAsync(
        Guid? actorId,
        string? action,
        string? resourceType,
        Guid? resourceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}