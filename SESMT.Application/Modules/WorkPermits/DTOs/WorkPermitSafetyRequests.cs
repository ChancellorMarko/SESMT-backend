using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitVersionRequest([Range(1, int.MaxValue)] int ExpectedVersion);

public sealed record WorkPermitReleaseRequest(
    [Range(1, int.MaxValue)] int ExpectedVersion,
    [Required, StringLength(2000, MinimumLength = 1)] string Conditions,
    DateTimeOffset AuthorizedFrom,
    DateTimeOffset AuthorizedUntil,
    bool Confirmation);

public sealed record WorkPermitAcknowledgementRequest(
    [Range(1, int.MaxValue)] int ExpectedVersion,
    [Range(1, int.MaxValue)] int ContentVersion,
    Guid ParticipantUserId,
    [Required, StringLength(160, MinimumLength = 1)] string ConfirmedName,
    [Required, StringLength(300, MinimumLength = 1)] string IdentificationMethod,
    bool Confirmation);

public sealed record WorkPermitDeclaration(Guid TemplateVersionId, int Version, string Text);
public sealed record WorkPermitDeclarationContext(Guid AppointmentId, int Version, Guid? LocationId, Guid? ResponsibleId,
    DateTimeOffset From, DateTimeOffset Until, IReadOnlyList<string> ServiceCodes, IReadOnlyList<Guid> ParticipantIds);
public sealed record WorkPermitDeclarationResponse(Guid PermitId, int ExpectedVersion, int ContentVersion,
    IReadOnlyList<WorkPermitDeclaration> Declarations, WorkPermitDeclarationContext? Context = null);

public sealed record WorkPermitObservationRequest([Range(1, int.MaxValue)] int ExpectedVersion,
    [Required, StringLength(4000, MinimumLength = 1)] string Observation);
public sealed record WorkPermitCompletionRequest([Range(1, int.MaxValue)] int ExpectedVersion,
    [Required, StringLength(4000, MinimumLength = 1)] string Summary, DateTimeOffset ActualEnd,
    IReadOnlyList<Domain.Modules.WorkPermits.Entities.WorkPermitChecklistAnswer> ClosingAnswers);
