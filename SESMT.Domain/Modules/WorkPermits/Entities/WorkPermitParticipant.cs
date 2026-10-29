using SESMT.Domain.Common.AggregateRoot;
using System.Text.Json;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class WorkPermitParticipant : AggregateRootBase
{
    public Guid WorkPermitId { get; private set; }
    public Guid UserId { get; private set; }
    public string Responsibility { get; private set; } = null!;
    public Guid AssignedBy { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public int? AcknowledgedVersion { get; private set; }
    public string AcknowledgementsJson { get; private set; } = "[]";
    public IReadOnlyList<WorkPermitAcknowledgement> Acknowledgements => JsonSerializer.Deserialize<WorkPermitAcknowledgement[]>(AcknowledgementsJson) ?? [];

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

    public bool HasAcknowledged(int contentVersion) => AcknowledgedVersion == contentVersion
        && AcknowledgedAt.HasValue && Acknowledgements.Any(item => item.ContentVersion == contentVersion);

    public void Acknowledge(Guid collectorId, int contentVersion, string confirmedName,
        string identificationMethod, string declaration, bool confirmation)
    {
        if (collectorId == Guid.Empty || contentVersion < 1 || !confirmation
            || string.IsNullOrWhiteSpace(confirmedName) || confirmedName.Trim().Length > 160
            || string.IsNullOrWhiteSpace(identificationMethod) || identificationMethod.Trim().Length > 300
            || string.IsNullOrWhiteSpace(declaration))
            throw new ArgumentException("Confirme a pessoa, a identificação e a declaração apresentadas.");
        var at = DateTimeOffset.UtcNow;
        var record = new WorkPermitAcknowledgement(Guid.NewGuid(), UserId, collectorId, contentVersion,
            confirmedName.Trim(), identificationMethod.Trim(), declaration, at);
        AcknowledgementsJson = JsonSerializer.Serialize(Acknowledgements.Append(record));
        AcknowledgedAt = at;
        AcknowledgedVersion = contentVersion;
        MarkAsUpdated(collectorId);
    }
}

public sealed record WorkPermitAcknowledgement(Guid Id, Guid ParticipantUserId, Guid CollectorId,
    int ContentVersion, string ConfirmedName, string IdentificationMethod, string Declaration, DateTimeOffset RecordedAt);
