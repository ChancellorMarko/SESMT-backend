using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Organization.Entities;

public class Team : AggregateRootBase
{
    public Guid OrganizationId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Guid? LocalId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid? ChiefId { get; private set; }
    public List<Guid> ParticipantIds { get; private set; } = new();
    public int Version { get; private set; } = 1;

    // OrganizationId is a logical grouping key, not an Organization aggregate/FK.
    // A standalone company is its own root; grouped companies use their configured root.
    public static Guid OrganizationFor(Guid companyId, Guid? organizationId)
    {
        if (companyId == Guid.Empty || organizationId == Guid.Empty)
            throw new ArgumentException("O contexto organizacional da empresa é inválido.");
        return organizationId ?? companyId;
    }

    protected Team() { }

    public Team(
    Guid organizationId,
    Guid? companyId,
    Guid? localId,
    string name,
    string? description,
    Guid? chiefId,
    List<Guid>? participantIds = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("O ID da organização é obrigatório.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da equipe é obrigatório.");

        if (chiefId == Guid.Empty)
            throw new ArgumentException("O ID do chefe não pode ser vazio.");

        OrganizationId = organizationId;
        CompanyId = companyId;
        LocalId = localId;
        Name = name.Trim();
        Description = description?.Trim();
        ChiefId = chiefId;

        var normalized = (participantIds ?? new List<Guid>()).Distinct().ToList();
        if (chiefId.HasValue)
            normalized.Remove(chiefId.Value);
        ParticipantIds = normalized;
    }

    public void Update(
    string name,
    Guid? localId,
    string? description,
    Guid? chiefId,
    List<Guid>? participantIds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da equipe é obrigatório.");

        if (chiefId == Guid.Empty)
            throw new ArgumentException("O ID do chefe não pode ser vazio.");

        Name = name.Trim();
        if (localId.HasValue) LocalId = localId;
        Description = description?.Trim();
        if (chiefId.HasValue) ChiefId = chiefId;
        if (participantIds is not null)
        {
            // Remove duplicatas e remove o próprio chefe da lista de participantes
            var normalized = participantIds.Distinct().ToList();
            if (ChiefId.HasValue)
                normalized.Remove(ChiefId.Value);
            ParticipantIds = normalized;
        }
        MarkAsUpdated();
        Version++;
    }

    public void AddParticipant(Guid workerId)
    {
        if (workerId == Guid.Empty)
            throw new ArgumentException("O ID do participante é obrigatório.");

        if (ChiefId != workerId && !ParticipantIds.Contains(workerId))
        {
            ParticipantIds.Add(workerId);
            MarkAsUpdated();
            Version++;
        }
    }

    public void RemoveParticipant(Guid workerId)
    {
        if (ParticipantIds.Remove(workerId))
        {
            MarkAsUpdated();
            Version++;
        }
    }

    public void Deactivate() { Version++; MarkAsDeleted(); }
}
