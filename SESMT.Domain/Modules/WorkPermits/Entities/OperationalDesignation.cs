using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Exceptions;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class OperationalDesignation : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid[] ServiceIds { get; private set; } = [];
    public Guid[] LocationIds { get; private set; } = [];
    public string Type { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string Origin { get; private set; } = null!;
    public Guid AssignedBy { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public int Version { get; private set; } = 1;

    private OperationalDesignation() { }

    public OperationalDesignation(Guid companyId, Guid personId, IEnumerable<Guid> serviceIds,
        IEnumerable<Guid> locationIds, string type, DateTimeOffset startsAt, DateTimeOffset endsAt,
        string origin, Guid assignedBy)
    {
        var services = serviceIds.Distinct().Order().ToArray();
        var locations = locationIds.Distinct().Order().ToArray();
        if (companyId == Guid.Empty || personId == Guid.Empty || assignedBy == Guid.Empty
            || services.Length == 0 || services.Any(id => id == Guid.Empty)
            || locations.Length == 0 || locations.Any(id => id == Guid.Empty))
            throw new ArgumentException("Pessoa, serviços, locais e ator são obrigatórios.");
        if (type is not ("atender" or "liberar" or "revisar" or "conferir"))
            throw new ArgumentException("Tipo deve ser atender, liberar, revisar ou conferir.");
        if (startsAt.Offset != TimeSpan.Zero || endsAt.Offset != TimeSpan.Zero || endsAt <= startsAt)
            throw new ArgumentException("Vigência deve estar em UTC e o fim ser posterior ao início.");
        if (string.IsNullOrWhiteSpace(origin) || origin.Length > 120)
            throw new ArgumentException("Origem é obrigatória e deve ter até 120 caracteres.");

        CompanyId = companyId;
        PersonId = personId;
        ServiceIds = services;
        LocationIds = locations;
        Type = type;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Origin = origin.Trim();
        AssignedBy = assignedBy;
        CreatedBy = assignedBy;
    }

    public void Revoke(Guid actorId, int expectedVersion)
    {
        if (RevokedAt.HasValue) return;
        if (expectedVersion != Version)
            throw new DomainException("A designação foi alterada. Recarregue a versão atual.");
        if (actorId == Guid.Empty) throw new ArgumentException("Ator inválido.");
        RevokedBy = actorId;
        RevokedAt = DateTimeOffset.UtcNow;
        Version++;
        MarkAsUpdated(actorId);
    }

    public bool IsEffectiveAt(DateTimeOffset instant) => Active && RevokedAt is null && StartsAt <= instant && instant < EndsAt;
}
