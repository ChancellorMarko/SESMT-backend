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
    public Guid? ActorId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string? ActorName { get; private set; } 
    public string Action { get; private set; }
    public string ResourceType { get; private set; }
    public Guid? ResourceId { get; private set; }
    public string? PreviousData { get; private set; }
    public string? NewData { get; private set; }
    public string? Context { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? PreviousHash { get; private set; }
    public string CurrentHash { get; private set; }
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
        string? previousHash)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("A ação de auditoria é obrigatória.");

        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("O tipo de recurso é obrigatório.");

        Action = action.Trim().ToUpperInvariant();
        ResourceType = resourceType.Trim().ToLowerInvariant();
        ResourceId = resourceId;
        ActorId = actorId;
        ActorEmail = actorEmail?.Trim().ToLowerInvariant();
        ActorName = actorName?.Trim();
        PreviousData = previousData;
        NewData = newData;
        Context = context;
        IpAddress = ipAddress?.Trim();
        UserAgent = userAgent?.Trim();
        PreviousHash = previousHash;
        OccurredAt = DateTimeOffset.UtcNow;

        CurrentHash = ComputeHash();
    }

    private string ComputeHash()
    {
        // Serializa de forma determinística todos os campos relevantes
        var payload = new StringBuilder()
            .Append(PreviousHash ?? "GENESIS").Append('|')
            .Append(OccurredAt.ToUnixTimeMilliseconds()).Append('|')
            .Append(ActorId?.ToString() ?? "-").Append('|')
            .Append(Action).Append('|')
            .Append(ResourceType).Append('|')
            .Append(ResourceId?.ToString() ?? "-").Append('|')
            .Append(PreviousData ?? "-").Append('|')
            .Append(NewData ?? "-")
            .ToString();

        var bytes = Encoding.UTF8.GetBytes(payload);
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
        string? userAgent = null)
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
            previousHash: previousHash);

    public static AuditLog ForLogin(
        Guid userId,
        string userEmail,
        string userName,
        bool success,
        string? previousHash,
        string? ipAddress = null,
        string? userAgent = null)
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
            previousHash: previousHash);
}