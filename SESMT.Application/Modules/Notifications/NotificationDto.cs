namespace SESMT.Application.Modules.Notifications;

public sealed record NotificationDto(Guid Id, string Type, Guid ResourceId, string Text,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
