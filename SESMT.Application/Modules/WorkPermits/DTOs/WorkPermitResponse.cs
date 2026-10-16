using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitResponse(
    Guid Id,
    Guid CompanyId,
    Guid? AppointmentId,
    string ServiceCode,
    string Title,
    string Status,
    int Version,
    DateTime CreatedAt,
    DateTimeOffset? ReleasedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? SuspendedAt,
    string? SuspensionReason,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<WorkPermitParticipantResponse> Participants)
{
    public static WorkPermitResponse From(WorkPermit permit) => new(
        permit.Id,
        permit.CompanyId,
        permit.AppointmentId,
        permit.ServiceCode,
        permit.Title,
        permit.Status,
        permit.Version,
        permit.CreatedAt,
        permit.ReleasedAt,
        permit.StartedAt,
        permit.SuspendedAt,
        permit.SuspensionReason,
        permit.CompletedAt,
        permit.CancelledAt,
        permit.CancellationReason,
        permit.Participants.Select(WorkPermitParticipantResponse.From).ToArray());
}
