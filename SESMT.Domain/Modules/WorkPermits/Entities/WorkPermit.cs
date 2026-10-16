using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.WorkPermits.Constants;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public class WorkPermit : AggregateRootBase
{
    private readonly List<WorkPermitTransition> _transitions = [];
    private readonly List<WorkPermitParticipant> _participants = [];

    public Guid CompanyId { get; private set; }
    public Guid? AppointmentId { get; private set; }
    public string ServiceCode { get; private set; } = null!;
    public int ContentVersion { get; private set; }
    public string Title { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public int Version { get; private set; }
    public Guid? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public Guid? StartedBy { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public Guid? SuspendedBy { get; private set; }
    public DateTimeOffset? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<WorkPermitTransition> Transitions => _transitions.AsReadOnly();
    public IReadOnlyCollection<WorkPermitParticipant> Participants => _participants.AsReadOnly();

    protected WorkPermit() { }

    public WorkPermit(Guid companyId, Guid appointmentId, string serviceCode, string title, Guid createdBy)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("A empresa da permissão de trabalho é obrigatória.");
        if (appointmentId == Guid.Empty)
            throw new ArgumentException("O agendamento da permissão de trabalho é obrigatório.");
        if (string.IsNullOrWhiteSpace(serviceCode))
            throw new ArgumentException("O serviço da permissão de trabalho é obrigatório.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da permissão de trabalho é obrigatório.");

        if (createdBy == Guid.Empty)
            throw new ArgumentException("O responsável pela abertura da permissão é inválido.");

        CompanyId = companyId;
        AppointmentId = appointmentId;
        ServiceCode = serviceCode.Trim().ToUpperInvariant();
        Title = title.Trim();
        CreatedBy = createdBy;
        Status = WorkPermitStatus.UnderReview;
        Version = 1;
        ContentVersion = 1;
        _transitions.Add(new WorkPermitTransition(Id, null, Status, "ABERTA", createdBy, DateTimeOffset.UtcNow, null, Version));
    }

    public void AddParticipant(WorkPermitParticipant participant, Guid actorId)
    {
        ArgumentNullException.ThrowIfNull(participant);
        EnsureActor(actorId);
        if (Status != WorkPermitStatus.UnderReview)
            throw new DomainException("Participantes só podem ser atribuídos durante a verificação.");
        if (participant.WorkPermitId != Id || _participants.Any(existing => existing.UserId == participant.UserId))
            throw new DomainException("Participante inválido ou já atribuído à permissão.");

        _participants.Add(participant);
        MarkAsUpdated(actorId);
    }

    public void Release(Guid actorId, bool readinessConditionsMet)
    {
        EnsureActor(actorId);
        EnsureStatus(WorkPermitStatus.UnderReview);
        EnsureReadiness(readinessConditionsMet);

        ReleasedBy = actorId;
        ReleasedAt = DateTimeOffset.UtcNow;
        MoveTo(WorkPermitStatus.Released, "LIBERADA", actorId);
    }

    public void Start(Guid actorId, bool readinessConditionsMet)
    {
        EnsureActor(actorId);
        EnsureStatus(WorkPermitStatus.Released);
        EnsureReadiness(readinessConditionsMet);

        StartedBy = actorId;
        StartedAt = DateTimeOffset.UtcNow;
        MoveTo(WorkPermitStatus.InProgress, "INICIADA", actorId);
    }

    public void Suspend(Guid actorId, string reason)
    {
        EnsureActor(actorId);
        EnsureStatus(WorkPermitStatus.InProgress);
        EnsureReason(reason, "O motivo da suspensão é obrigatório.");

        SuspendedBy = actorId;
        SuspendedAt = DateTimeOffset.UtcNow;
        SuspensionReason = reason.Trim();
        MoveTo(WorkPermitStatus.Suspended, "SUSPENSA", actorId, SuspensionReason);
    }

    public void Resume(Guid actorId, bool readinessConditionsMet)
    {
        EnsureActor(actorId);
        EnsureStatus(WorkPermitStatus.Suspended);
        EnsureReadiness(readinessConditionsMet);

        MoveTo(WorkPermitStatus.InProgress, "RETOMADA", actorId);
    }

    public void Complete(Guid actorId)
    {
        EnsureActor(actorId);
        EnsureStatus(WorkPermitStatus.InProgress);

        CompletedBy = actorId;
        CompletedAt = DateTimeOffset.UtcNow;
        MoveTo(WorkPermitStatus.Completed, "CONCLUIDA", actorId);
    }

    public void Cancel(Guid actorId, string reason)
    {
        EnsureActor(actorId);
        EnsureStatus(WorkPermitStatus.UnderReview, WorkPermitStatus.Released);
        EnsureReason(reason, "O motivo do cancelamento é obrigatório.");

        CancelledBy = actorId;
        CancelledAt = DateTimeOffset.UtcNow;
        CancellationReason = reason.Trim();
        MoveTo(WorkPermitStatus.Cancelled, "CANCELADA", actorId, CancellationReason);
    }

    private void MoveTo(string newStatus, string action, Guid actorId, string? reason = null)
    {
        var previousStatus = Status;
        Status = newStatus;
        Version++;
        MarkAsUpdated(actorId);
        _transitions.Add(new WorkPermitTransition(Id, previousStatus, newStatus, action, actorId, DateTimeOffset.UtcNow, reason, Version));
    }

    private static void EnsureActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new DomainException("O responsável pela ação é obrigatório.");
    }

    private static void EnsureReason(string reason, string message)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException(message);
    }

    private static void EnsureReadiness(bool readinessConditionsMet)
    {
        if (!readinessConditionsMet)
            throw new DomainException("A permissão possui pendências que impedem esta ação.");
    }

    private void EnsureStatus(params string[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
            throw new DomainException($"A ação não é permitida no status atual ({Status}).");
    }
}
