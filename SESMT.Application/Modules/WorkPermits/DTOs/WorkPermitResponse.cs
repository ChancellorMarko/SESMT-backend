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
    IReadOnlyList<WorkPermitParticipantResponse> Participants,
    IReadOnlyList<Guid> TemplateVersionIds,
    IReadOnlyList<WorkPermitChecklistAnswer> ChecklistAnswers,
    int ContentVersion = 1, DateTimeOffset? AuthorizedFrom = null, DateTimeOffset? AuthorizedUntil = null, string? ReleaseConditions = null,
    string? CompletionSummary = null, DateTimeOffset? ActualEnd = null, string ReviewStatus = "NAO_ENVIADA",
    IReadOnlyList<WorkPermitChecklistAnswer>? ClosingAnswers = null, IReadOnlyList<WorkPermitObservation>? Observations = null)
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
        permit.Participants.Select(WorkPermitParticipantResponse.From).ToArray(),
        permit.TemplateVersionIds.ToArray(),
        permit.ChecklistAnswers.ToArray(), permit.ContentVersion, permit.AuthorizedFrom, permit.AuthorizedUntil, permit.ReleaseConditions,
        permit.CompletionSummary, permit.ActualEnd, permit.ReviewStatus, permit.ClosingAnswers, permit.Observations);
}
