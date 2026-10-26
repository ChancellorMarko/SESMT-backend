using System.Net;
using System.Security.Cryptography;
using System.Text;
using SESMT.Domain.Modules.Audit.Constants;

namespace SESMT.Domain.Modules.Audit.Entities;

/// <summary>
/// Registro imutável de auditoria (RF-061, §12).
/// NÃO herda de EntityBase: é um fato ocorrido, não um objeto com ciclo de vida.
/// NÃO possui métodos de Update: é append-only por design.
/// A integridade é garantida por cadeia de hash (HashPrevious → HashCurrent).
/// </summary>
public class AuditLog
{
    public long Id { get; private set; }
    public Guid EventId { get; private set; }
    public int HashVersion { get; private set; } = 1;
    public Guid? ActorId { get; private set; }
    public Guid? DeclarantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string? ActorName { get; private set; }
    public string Action { get; private set; } = null!;
    public string ResourceType { get; private set; } = null!;
    public Guid? ResourceId { get; private set; }
    public int? ResourceVersion { get; private set; }
    public string? Result { get; private set; }
    public string? TraceId { get; private set; }
    public string? PreviousData { get; private set; }
    public string? NewData { get; private set; }
    public string? Context { get; private set; }
    public IPAddress? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? PreviousHash { get; private set; }
    public string CurrentHash { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    protected AuditLog()
    {
        Action = null!;
        ResourceType = null!;
        CurrentHash = null!;
    }

    public AuditLog(
        string action,
        string resourceType,
        Guid? resourceId,
        Guid? actorId,
        string? actorEmail,
        string? actorName,
        string? previousData,
        string? newData,
        string? context,
        string? ipAddress,
        string? userAgent,
        string? previousHash,
        Guid? companyId = null,
        Guid? declarantId = null,
        int? resourceVersion = null,
        string? result = null,
        string? traceId = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("A ação de auditoria é obrigatória.");

        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("O tipo de recurso é obrigatório.");

        Action = action.Trim().ToUpperInvariant();
        ResourceType = resourceType.Trim().ToLowerInvariant();
        EventId = Guid.NewGuid();
        HashVersion = 2;
        ResourceId = resourceId;
        ActorId = actorId;
        DeclarantId = declarantId;
        CompanyId = companyId;
        ResourceVersion = resourceVersion;
        Result = result?.Trim().ToUpperInvariant();
        TraceId = traceId?.Trim();
        ActorEmail = actorEmail?.Trim().ToLowerInvariant();
        ActorName = actorName?.Trim();
        PreviousData = previousData;
        NewData = newData;
        Context = context;
        IpAddress = ipAddress != null ? IPAddress.Parse(ipAddress) : null;
        UserAgent = userAgent?.Trim();
        PreviousHash = previousHash;
        OccurredAt = DateTimeOffset.UtcNow;

        CurrentHash = ComputeHash();
    }

    private string ComputeHash()
    {
        var payload = new StringBuilder()
            .Append(PreviousHash ?? "GENESIS").Append('|')
            .Append(OccurredAt.ToUnixTimeMilliseconds()).Append('|')
            .Append(ActorId?.ToString() ?? "-").Append('|');

        if (HashVersion >= 2)
            payload.Append(EventId).Append('|')
                .Append(DeclarantId?.ToString() ?? "-").Append('|')
                .Append(CompanyId?.ToString() ?? "-").Append('|');

        payload.Append(ActorEmail ?? "-").Append('|')
            .Append(ActorName ?? "-").Append('|')
            .Append(Action).Append('|')
            .Append(ResourceType).Append('|')
            .Append(ResourceId?.ToString() ?? "-").Append('|');

        if (HashVersion >= 2)
            payload.Append(ResourceVersion?.ToString() ?? "-").Append('|')
                .Append(Result ?? "-").Append('|')
                .Append(TraceId ?? "-").Append('|');

        payload
            .Append(PreviousData ?? "-").Append('|')
            .Append(NewData ?? "-").Append('|')
            .Append(Context ?? "-").Append('|')
            .Append(IpAddress?.ToString() ?? "-").Append('|')
            .Append(UserAgent ?? "-")
            .ToString();

        var bytes = Encoding.UTF8.GetBytes(payload.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    // ─── Verificação de integridade (usado em auditoria) ────────────────────
    /// <summary>
    /// Recalcula o hash e compara com o armazenado.
    /// Se diferir, o registro foi adulterado.
    /// </summary>
    public bool IsIntegrityValid()
    {
        var recomputed = ComputeHash();
        return string.Equals(recomputed, CurrentHash, StringComparison.OrdinalIgnoreCase);
    }

    public static AuditLog ForCreate(
        string resourceType,
        Guid resourceId,
        Guid? actorId,
        string? actorEmail,
        string? actorName,
        string newDataJson,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null)
        => new(
            action: AuditAction.Create,
            resourceType: resourceType,
            resourceId: resourceId,
            actorId: actorId,
            actorEmail: actorEmail,
            actorName: actorName,
            previousData: null,
            newData: newDataJson,
            context: null,
            ipAddress: ipAddress,
            userAgent: userAgent,
            previousHash: previousHash);

    public static AuditLog ForUpdate(
        string resourceType,
        Guid resourceId,
        Guid? actorId,
        string? actorEmail,
        string? actorName,
        string previousDataJson,
        string newDataJson,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null)
        => new(
            action: AuditAction.Update,
            resourceType: resourceType,
            resourceId: resourceId,
            actorId: actorId,
            actorEmail: actorEmail,
            actorName: actorName,
            previousData: previousDataJson,
            newData: newDataJson,
            context: null,
            ipAddress: ipAddress,
            userAgent: userAgent,
            previousHash: previousHash);

    public static AuditLog ForDelete(
        string resourceType,
        Guid resourceId,
        Guid? actorId,
        string? actorEmail,
        string? actorName,
        string previousDataJson,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null)
        => new(
            action: AuditAction.Delete,
            resourceType: resourceType,
            resourceId: resourceId,
            actorId: actorId,
            actorEmail: actorEmail,
            actorName: actorName,
            previousData: previousDataJson,
            newData: null,
            context: null,
            ipAddress: ipAddress,
            userAgent: userAgent,
            previousHash: previousHash);

    public static AuditLog ForSensitiveView(
        string resourceType,
        Guid resourceId,
        Guid? actorId,
        string? actorEmail,
        string? actorName,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? companyId = null,
        int? resourceVersion = null,
        string? traceId = null)
        => new(
            action: AuditAction.SensitiveView,
            resourceType: resourceType,
            resourceId: resourceId,
            actorId: actorId,
            actorEmail: actorEmail,
            actorName: actorName,
            previousData: null,
            newData: null,
            context: null,
            ipAddress: ipAddress,
            userAgent: userAgent,
            previousHash: previousHash,
            companyId: companyId,
            resourceVersion: resourceVersion,
            result: "SUCESSO",
            traceId: traceId);

    public static AuditLog ForLogin(
        Guid userId,
        string userEmail,
        string userName,
        bool success,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null,
        string? traceId = null)
        => new(
            action: success ? AuditAction.Login : AuditAction.LoginFailed,
            resourceType: AuditResource.User,
            resourceId: userId,
            actorId: userId,
            actorEmail: userEmail,
            actorName: userName,
            previousData: null,
            newData: null,
            context: null,
            ipAddress: ipAddress,
            userAgent: userAgent,
            previousHash: previousHash,
            result: success ? "SUCESSO" : "NEGADO",
            traceId: traceId);

    public static AuditLog ForCustom(
        string action,
        string resourceType,
        Guid? resourceId,
        Guid? actorId,
        string? actorEmail,
        string? actorName,
        string? previousData,
        string? newData,
        string? context,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? companyId = null,
        Guid? declarantId = null,
        int? resourceVersion = null,
        string? result = null,
        string? traceId = null)
        => new(
            action: action,
            resourceType: resourceType,
            resourceId: resourceId,
            actorId: actorId,
            actorEmail: actorEmail,
            actorName: actorName,
            previousData: previousData,
            newData: newData,
            context: context,
            ipAddress: ipAddress,
            userAgent: userAgent,
            previousHash: previousHash,
            companyId: companyId,
            declarantId: declarantId,
            resourceVersion: resourceVersion,
            result: result,
            traceId: traceId);
}
