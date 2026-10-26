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
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.Documents.Constants;

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

        typeof(Alert),
        typeof(SystemParameter),
        typeof(WorkPermit),
        typeof(WorkPermitParticipant),
    };

    // Campos que NUNCA devem ir para o JSON de auditoria
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "MfaSecretEncrypted",
        "CpfEncryption",
        "RefreshTokenHash",
        "TokenHash",
        "CpfHash",
        "DateOfBirth",
        "Metadata",
        "Justification",
        "ChecklistAnswersJson",
        "TemplateVersionIdsJson",
        "ObjectKey",
        "FileHash",
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
            var log = BuildLogFor(entry, lastHash, db);
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
        string? previousHash,
        DbContext db)
    {
        var entityType = entry.Entity.GetType();
        var resourceType = ToResourceName(entityType);

        if (entry.Entity is WorkPermit permit)
        {
            var oldStatus = entry.State == EntityState.Added ? null : entry.OriginalValues[nameof(WorkPermit.Status)] as string;
            var action = entry.State == EntityState.Added
                ? "PT_ABERTA"
                : ResolvePermitAction(oldStatus, permit.Status,
                    entry.Property(nameof(WorkPermit.ContentVersion)).IsModified);

            return AuditLog.ForCustom(action, AuditResource.WorkPermit, permit.Id,
                _currentUser.UserId, _currentUser.Email, _currentUser.Name,
                previousData: null, newData: null, context: null, previousHash,
                ipAddress: _currentUser.IpAddress, userAgent: _currentUser.UserAgent,
                companyId: permit.CompanyId, resourceVersion: permit.Version,
                result: "SUCESSO", traceId: _currentUser.TraceId);
        }

        if (entry.Entity is WorkPermitParticipant participant)
        {
            if (!entry.Property(nameof(WorkPermitParticipant.AcknowledgedVersion)).IsModified)
                return null;

            var companyId = db.ChangeTracker.Entries<WorkPermit>()
                .FirstOrDefault(e => e.Entity.Id == participant.WorkPermitId)?.Entity.CompanyId;
            return AuditLog.ForCustom("CIENCIA_REGISTRADA", AuditResource.WorkPermit,
                participant.WorkPermitId, _currentUser.UserId, _currentUser.Email, _currentUser.Name,
                previousData: null, newData: null, context: null, previousHash,
                ipAddress: _currentUser.IpAddress, userAgent: _currentUser.UserAgent,
                companyId: companyId, declarantId: participant.UserId,
                resourceVersion: participant.AcknowledgedVersion, result: "SUCESSO",
                traceId: _currentUser.TraceId);
        }

        if (entry.Entity is Document or DocumentVersion)
        {
            return AuditLog.ForCustom(ResolveDocumentAction(entry), resourceType,
                TryGetGuid(entry, "Id"), _currentUser.UserId, _currentUser.Email, _currentUser.Name,
                previousData: null, newData: null, context: null, previousHash,
                ipAddress: _currentUser.IpAddress, userAgent: _currentUser.UserAgent,
                companyId: ResolveCompanyId(entry, db),
                resourceVersion: TryGetInt(entry, nameof(DocumentVersion.VersionNumber)),
                result: "SUCESSO", traceId: _currentUser.TraceId);
        }

        var resourceId = TryGetGuid(entry, "Id");
        if (resourceId is null) return null;   // sem Id? não audita

        var actor = _currentUser;

        var auditAction = entry.State switch
        {
            EntityState.Added => AuditAction.Create,
            EntityState.Modified => AuditAction.Update,
            EntityState.Deleted => AuditAction.Delete,
            _ => null,
        };
        if (auditAction is null) return null;

        return AuditLog.ForCustom(auditAction, resourceType, resourceId, actor.UserId,
            actor.Email, actor.Name,
            entry.State == EntityState.Added ? null : SerializeEntry(entry.OriginalValues),
            entry.State == EntityState.Deleted ? null : SerializeEntry(entry.CurrentValues),
            context: null, previousHash,
            ipAddress: actor.IpAddress, userAgent: actor.UserAgent,
            companyId: ResolveCompanyId(entry, db),
            resourceVersion: TryGetInt(entry, "Version"),
            result: "SUCESSO", traceId: actor.TraceId);
    }

    private static string ResolveDocumentAction(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is DocumentVersion)
        {
            var state = entry.Property(nameof(DocumentVersion.State)).CurrentValue as string;
            if (entry.State == EntityState.Added) return "UPLOAD";
            return state switch
            {
                DocumentVersionState.InReview => "CONFERENCIA_SOLICITADA",
                DocumentVersionState.Approved => AuditAction.Approve,
                DocumentVersionState.Rejected => "DEVOLUCAO",
                DocumentVersionState.Current => "PUBLICACAO",
                _ => "CORRECAO_DOCUMENTAL",
            };
        }

        if (entry.State == EntityState.Added) return AuditAction.Create;
        var status = entry.Property(nameof(Document.Status)).CurrentValue as string;
        return status switch
        {
            "EM_REVISAO" => "CONFERENCIA_SOLICITADA",
            "APROVADO" => "CONFERENCIA_APROVADA",
            "RASCUNHO" => "CONFERENCIA_DEVOLVIDA",
            _ => AuditAction.Update,
        };
    }

    private static Guid? ResolveCompanyId(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        DbContext db)
    {
        var companyId = TryGetGuid(entry, "CompanyId");
        if (companyId.HasValue) return companyId;
        if (entry.Entity is DocumentVersion version)
            return db.ChangeTracker.Entries<Document>()
                .FirstOrDefault(e => e.Entity.Id == version.DocumentId)?.Entity.CompanyId;
        return null;
    }

    private static int? TryGetInt(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string propertyName)
    {
        var value = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName)?.CurrentValue;
        return value is int number ? number : null;
    }

    private static string ResolvePermitAction(string? oldStatus, string newStatus, bool checklistChanged)
    {
        if (oldStatus != newStatus)
        {
            return newStatus switch
            {
                WorkPermitStatus.Released => "PT_LIBERADA",
                WorkPermitStatus.InProgress when oldStatus == WorkPermitStatus.Suspended => "PT_RETOMADA",
                WorkPermitStatus.InProgress => "PT_INICIADA",
                WorkPermitStatus.Suspended => "PT_SUSPENSA",
                WorkPermitStatus.Completed => "PT_CONCLUIDA",
                WorkPermitStatus.Cancelled => "PT_CANCELADA",
                _ => "PT_STATUS_ALTERADO",
            };
        }

        return checklistChanged ? "CHECKLIST_ALTERADO" : "PT_ATUALIZADA";
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
        nameof(WorkPermit) => AuditResource.WorkPermit,
        nameof(WorkPermitParticipant) => AuditResource.WorkPermit,
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
