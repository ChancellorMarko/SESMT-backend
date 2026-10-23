using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Administration.Entities;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Notifications.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Infrastructure.Persistence.Interceptors;

public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Entidades cujo ciclo de vida é auditado automaticamente.
    /// AuditLog em si é excluído (obviamente). UserSession e PasswordResetToken
    /// também são excluídos para não vazar info de sessão/token.
    /// </summary>
    private static readonly HashSet<Type> AuditedTypes = new()
    {
        typeof(User),
        typeof(Role),
        typeof(Permission),
        typeof(RolePermission),
        typeof(UserRole),
        typeof(UserScope),

        typeof(Company),
        typeof(Unit),
        typeof(Department),
        typeof(Position),
        typeof(Team),

        typeof(Worker),
        typeof(WorkerHistory),

        typeof(Document),
        typeof(DocumentType),
        typeof(DocumentVersion),
        typeof(DocumentLink),
        typeof(StoredFile),

        typeof(Alert),
        typeof(SystemParameter),
    };

    // Campos que NUNCA devem ir para o JSON de auditoria
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "MfaSecretEncrypted",
        "CpfEncryption",
        "RefreshTokenHash",
        "TokenHash",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public AuditInterceptor(
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var db = eventData.Context;
        if (db is null) return result;

        // 1. Coletar mudanças relevantes
        var changes = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added
                            or EntityState.Modified
                            or EntityState.Deleted)
            .Where(e => AuditedTypes.Contains(e.Entity.GetType()))
            .ToList();

        if (changes.Count == 0) return result;

        // 2. Pegar o hash do último log existente (base da cadeia)
        var lastHash = await db.Set<AuditLog>()
            .OrderByDescending(a => a.Id)
            .Select(a => a.CurrentHash)
            .FirstOrDefaultAsync(cancellationToken);

        // 3. Construir logs e encadear o hash dentro do batch
        var logs = new List<AuditLog>(changes.Count);

        foreach (var entry in changes)
        {
            var log = BuildLogFor(entry, lastHash);
            if (log is null) continue;

            logs.Add(log);
            lastHash = log.CurrentHash;   // próximo log aponta pra este
        }

        // 4. Adicionar à fila — o SaveChanges do EF insere tudo na mesma transação
        if (logs.Count > 0)
            await db.Set<AuditLog>().AddRangeAsync(logs, cancellationToken);

        return result;
    }

    private AuditLog? BuildLogFor(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string? previousHash)
    {
        var entityType = entry.Entity.GetType();
        var resourceType = ToResourceName(entityType);

        var resourceId = TryGetGuid(entry, "Id");
        if (resourceId is null) return null;   // sem Id? não audita

        var actor = _currentUser;

        return entry.State switch
        {
            EntityState.Added => AuditLog.ForCreate(
                resourceType: resourceType,
                resourceId: resourceId.Value,
                actorId: actor.UserId,
                actorEmail: actor.Email,
                actorName: actor.Name,
                newDataJson: SerializeEntry(entry.CurrentValues),
                previousHash: previousHash,
                ipAddress: actor.IpAddress,
                userAgent: actor.UserAgent),

            EntityState.Modified => AuditLog.ForUpdate(
                resourceType: resourceType,
                resourceId: resourceId.Value,
                actorId: actor.UserId,
                actorEmail: actor.Email,
                actorName: actor.Name,
                previousDataJson: SerializeEntry(entry.OriginalValues),
                newDataJson: SerializeEntry(entry.CurrentValues),
                previousHash: previousHash,
                ipAddress: actor.IpAddress,
                userAgent: actor.UserAgent),

            EntityState.Deleted => AuditLog.ForDelete(
                resourceType: resourceType,
                resourceId: resourceId.Value,
                actorId: actor.UserId,
                actorEmail: actor.Email,
                actorName: actor.Name,
                previousDataJson: SerializeEntry(entry.OriginalValues),
                previousHash: previousHash,
                ipAddress: actor.IpAddress,
                userAgent: actor.UserAgent),

            _ => null,
        };
    }

    private static string SerializeEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in values.Properties)
        {
            if (SensitiveFields.Contains(property.Name)) continue;
            if (typeof(byte[]).IsAssignableFrom(property.ClrType)) continue;

            var value = values[property];
            if (value is null) continue;

            // Value Objects: serializa apenas o Value interno
            if (value.GetType().Namespace?.StartsWith("SESMT.Domain.ValueObjects") == true)
            {
                var innerValue = value.GetType().GetProperty("Value")?.GetValue(value);
                dict[property.Name] = innerValue;
            }
            else
            {
                dict[property.Name] = value;
            }
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static string ToResourceName(Type entityType) => entityType.Name switch
    {
        nameof(User) => AuditResource.User,
        nameof(Role) => AuditResource.Role,
        nameof(Permission) => AuditResource.Permission,
        nameof(Company) => AuditResource.Company,
        nameof(Unit) => AuditResource.Unit,
        nameof(Department) => AuditResource.Department,
        nameof(Position) => AuditResource.Position,
        nameof(Worker) => AuditResource.Worker,
        nameof(Document) => AuditResource.Document,
        nameof(DocumentVersion) => AuditResource.DocumentVersion,
        nameof(Alert) => AuditResource.Alert,
        nameof(SystemParameter) => AuditResource.SystemParameter,
        _ => entityType.Name.ToLowerInvariant(),
    };

    private static Guid? TryGetGuid(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string propertyName)
    {
        var prop = entry.Properties
            .FirstOrDefault(p => p.Metadata.Name == propertyName);

        return prop?.CurrentValue as Guid?;
    }
}
