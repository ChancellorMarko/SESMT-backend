using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Notifications.Entities;

public class Alert : AggregateRootBase
{
    public string Type { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string Severity { get; private set; } = null!;
    public Guid? DestinationUserId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public string Status { get; private set; } = null!;
    public int SeverityLevel { get; private set; }

    protected Alert() { }

    public Alert(string type, string entityType, Guid entityId, Guid sourceEventId, Guid destinationUserId, string channel, string title, string message, DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(entityType) || entityId == Guid.Empty
            || sourceEventId == Guid.Empty || destinationUserId == Guid.Empty || string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Os dados e destinatário da notificação são obrigatórios.");
        Type = type;
        EntityType = entityType;
        EntityId = entityId;
        SourceEventId = sourceEventId;
        Severity = "INFO";
        DestinationUserId = destinationUserId;
        Channel = channel;
        Title = title;
        Message = message;
        SentAt = sentAt;
        Status = "NOVA";
        SeverityLevel = 0;
    }

    public void MarkAsRead(Guid actorId)
    {
        if (actorId == Guid.Empty || DestinationUserId != actorId)
            throw new InvalidOperationException("A notificação não pertence a este usuário.");
        if (ReadAt.HasValue) return;
        ReadAt = DateTimeOffset.UtcNow;
        Status = "LIDA";
        MarkAsUpdated(actorId);
    }

    public void UpdateAlert(string type, string entityType, Guid entityId, string severity, Guid sourceEventId, Guid destinationUserId, string channel, string title, string message, DateTimeOffset sentAt, DateTimeOffset? readAt = null, string status = "Nova", int severityLevel = 0)
    {
        Type = type;
        EntityType = entityType;
        EntityId = entityId;
        SourceEventId = sourceEventId;
        Severity = severity;
        DestinationUserId = destinationUserId;
        Channel = channel;
        Title = title;
        Message = message;
        SentAt = sentAt;
        ReadAt = readAt;
        Status = status;
        SeverityLevel = severityLevel;

        MarkAsUpdated();
    }
}
