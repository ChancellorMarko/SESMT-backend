using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class WorkPermitParticipant : AggregateRootBase
{
    public Guid WorkPermitId { get; private set; }
    public Guid UserId { get; private set; }
    public string Responsibility { get; private set; } = null!;
    public Guid AssignedBy { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public int? AcknowledgedVersion { get; private set; }

    private WorkPermitParticipant() { }

    public WorkPermitParticipant(Guid workPermitId, Guid userId, string responsibility, Guid assignedBy)
    {
        if (workPermitId == Guid.Empty || userId == Guid.Empty || assignedBy == Guid.Empty)
            throw new ArgumentException("PT, participante e responsável pela atribuição são obrigatórios.");
        if (string.IsNullOrWhiteSpace(responsibility))
            throw new ArgumentException("A responsabilidade do participante é obrigatória.");

        WorkPermitId = workPermitId;
        UserId = userId;
        Responsibility = responsibility.Trim();
        AssignedBy = assignedBy;
        CreatedBy = assignedBy;
    }

    public void Acknowledge(Guid userId, int permitVersion)
    {
        if (userId != UserId)
            throw new InvalidOperationException("Somente o próprio participante pode registrar ciência.");
        if (permitVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(permitVersion));

        AcknowledgedAt = DateTimeOffset.UtcNow;
        AcknowledgedVersion = permitVersion;
        MarkAsUpdated(userId);
    }
}
