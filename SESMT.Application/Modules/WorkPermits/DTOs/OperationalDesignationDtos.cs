namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record CreateOperationalDesignationRequest(Guid PersonId, IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> LocationIds, string Type, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Origin);
public sealed record RevokeOperationalDesignationRequest(int ExpectedVersion);
public sealed record OperationalDesignationResponse(Guid Id, Guid PersonId, IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> LocationIds, string Type, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string Origin, bool Active, int Version, Guid AssignedBy, Guid? RevokedBy, DateTimeOffset? RevokedAt)
{
    public static OperationalDesignationResponse From(Domain.Modules.WorkPermits.Entities.OperationalDesignation x) =>
        new(x.Id, x.PersonId, x.ServiceIds, x.LocationIds, x.Type, x.StartsAt, x.EndsAt, x.Origin,
            x.Active && x.RevokedAt is null, x.Version, x.AssignedBy, x.RevokedBy, x.RevokedAt);
}
