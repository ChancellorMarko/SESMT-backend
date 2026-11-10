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
    bool Confirmation,
    [Required, RegularExpression("^[a-f0-9]{64}$")] string? ContentHash = null);

public sealed record WorkPermitDeclaration(Guid TemplateVersionId, int Version, string Text, Guid? TemplateId = null);
public sealed record WorkPermitDeclarationContext(Guid AppointmentId, int Version, Guid? LocationId, Guid? ResponsibleId,
    DateTimeOffset From, DateTimeOffset Until, IReadOnlyList<string> ServiceCodes, IReadOnlyList<Guid> ParticipantIds, Guid? TeamId = null);
public sealed record WorkPermitDeclarationResponse(Guid PermitId, int ExpectedVersion, int ContentVersion,
    IReadOnlyList<WorkPermitDeclaration> Declarations, WorkPermitDeclarationContext? Context = null,
    string? CanonicalContent = null, string? ContentHash = null);

public sealed record WorkPermitObservationRequest([Range(1, int.MaxValue)] int ExpectedVersion,
    [Required, StringLength(4000, MinimumLength = 1)] string Observation);
/// <summary>Final review: Decision is APROVADA or DEVOLVIDA; a return requires Note.</summary>
public sealed record WorkPermitReviewRequest([Range(1, int.MaxValue)] int ExpectedVersion,
    [Required] string Decision, [StringLength(2000)] string? Note);
/// <summary>Review state for the caller: what the permit shows and what this person may do now.</summary>
public sealed record WorkPermitReviewState(Guid PermitId, int Version, string ReviewStatus, Guid? SubmittedBy, DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string? ReviewNote, bool CanReview, bool CanResubmit,
    IReadOnlyList<Domain.Modules.WorkPermits.Entities.WorkPermitClosingRecord> PreviousClosings);
public sealed record WorkPermitCompletionRequest([Range(1, int.MaxValue)] int ExpectedVersion,
    [Required, StringLength(4000, MinimumLength = 1)] string Summary, DateTimeOffset ActualEnd,
    IReadOnlyList<Domain.Modules.WorkPermits.Entities.WorkPermitChecklistAnswer> ClosingAnswers);
