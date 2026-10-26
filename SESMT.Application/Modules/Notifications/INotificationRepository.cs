using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.Notifications;

public interface INotificationRepository
{
    Task<IReadOnlyList<NotificationDto>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct);
    Task EnqueueWorkPermitReviewersAsync(WorkPermit permit, Guid sourceEventId, string type, CancellationToken ct);
    Task EnqueueDocumentReturnAsync(Guid documentId, Guid sourceEventId, Guid recipientUserId, CancellationToken ct);
}
