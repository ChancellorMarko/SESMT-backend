using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.Notifications;

public sealed class NotificationService(INotificationRepository repository)
{
    public Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken ct) =>
        repository.ListForUserAsync(userId, ct);

    public Task<bool> MarkAsReadAsync(Guid userId, Guid id, CancellationToken ct) =>
        repository.MarkAsReadAsync(userId, id, ct);

    public Task EnqueueReviewersAsync(WorkPermit permit, Guid sourceEventId, string type, CancellationToken ct) =>
        repository.EnqueueWorkPermitReviewersAsync(permit, sourceEventId, type, ct);

    public Task EnqueueDocumentReturnAsync(Guid documentId, Guid sourceEventId, Guid recipientUserId, CancellationToken ct) =>
        repository.EnqueueDocumentReturnAsync(documentId, sourceEventId, recipientUserId, ct);
}
