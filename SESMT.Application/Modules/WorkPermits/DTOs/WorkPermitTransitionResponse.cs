using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitTransitionResponse(
    Guid Id,
    string? PreviousStatus,
    string NewStatus,
    string Action,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? Reason,
    int Version)
{
    public static WorkPermitTransitionResponse From(WorkPermitTransition transition) => new(
        transition.Id,
        transition.PreviousStatus,
        transition.NewStatus,
        transition.Action,
        transition.ActorId,
        transition.OccurredAt,
        transition.Reason,
        transition.Version);
}
