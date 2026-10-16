namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class WorkPermitTransition
{
    public Guid Id { get; private set; }
    public Guid WorkPermitId { get; private set; }
    public string? PreviousStatus { get; private set; }
    public string NewStatus { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Reason { get; private set; }
    public int Version { get; private set; }

    private WorkPermitTransition() { }

    internal WorkPermitTransition(Guid workPermitId, string? previousStatus, string newStatus, string action, Guid actorId, DateTimeOffset occurredAt, string? reason, int version)
    {
        Id = Guid.NewGuid();
        WorkPermitId = workPermitId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Action = action;
        ActorId = actorId;
        OccurredAt = occurredAt;
        Reason = reason;
        Version = version;
    }
}
