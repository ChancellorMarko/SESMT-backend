using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitParticipantResponse(Guid UserId, string Responsibility, DateTimeOffset? AcknowledgedAt, int? AcknowledgedVersion,
    IReadOnlyList<WorkPermitAcknowledgement>? Acknowledgements = null)
{
    public static WorkPermitParticipantResponse From(WorkPermitParticipant participant) =>
        new(participant.UserId, participant.Responsibility, participant.AcknowledgedAt, participant.AcknowledgedVersion, participant.Acknowledgements);
}
