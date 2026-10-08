using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Notifications.Entities;

public class Alert : AggregateRootBase
{
    public string Type { get; private set; }
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public string Severity { get; private set; }
    public Guid? DestinationUserId { get; private set; }
    public string Channel { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public DateTime SentAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string Status { get; private set; }
    public int SeverityLevel { get; private set; }

    protected Alert() { }

    public Alert(string type, string entityType, Guid entityId, string severity, Guid? destinationUserId, string channel, string title, string message, DateTime sentAt, DateTime? readAt, string status, int severityLevel)
    {
        Type = type;
        EntityType = entityType;
        EntityId = entityId;
        Severity = severity;
        DestinationUserId = destinationUserId;
        Channel = channel;
        Title = title;
        Message = message;
        SentAt = sentAt;
        ReadAt = readAt;
        Status = status;
        SeverityLevel = severityLevel;
    }

    public void UpdateAlert(string type, string entityType, Guid entityId, string severity, Guid? destinationUserId, string channel, string title, string message, DateTime sentAt, DateTime? readAt, string status, int severityLevel)
    {
        Type = type;
        EntityType = entityType;
        EntityId = entityId;
        Severity = severity;
        DestinationUserId = destinationUserId;
        Channel = channel;
        Title = title;
        Message = message;
        SentAt = sentAt;
        ReadAt = readAt;
        Status = status;
        SeverityLevel = severityLevel;
    }
}