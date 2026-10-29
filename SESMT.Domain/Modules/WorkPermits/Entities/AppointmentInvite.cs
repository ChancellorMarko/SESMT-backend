using SESMT.Domain.Common.AggregateRoot;
using SESMT.Domain.Exceptions;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class AppointmentInvite : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid LocationId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public int Version { get; private set; } = 1;
    private AppointmentInvite() { }
    public AppointmentInvite(Guid companyId, Guid teamId, Guid locationId, string tokenHash, DateTimeOffset expiresAt, Guid actorId, DateTimeOffset now)
    {
        if (new[] {companyId, teamId, locationId, actorId}.Any(id => id == Guid.Empty)
            || tokenHash.Length != 64 || !tokenHash.All(Uri.IsHexDigit) || expiresAt.Offset != TimeSpan.Zero || expiresAt <= now)
            throw new ArgumentException("Convite, origem ou validade inválidos.");
        CompanyId = companyId; TeamId = teamId; LocationId = locationId; TokenHash = tokenHash;
        ExpiresAt = expiresAt; CreatedBy = actorId;
    }
    public bool IsUsableAt(DateTimeOffset now) => Active && DeletedAt is null && RevokedAt is null && now < ExpiresAt;
    public void Revoke(Guid actorId, int expectedVersion, DateTimeOffset now)
    {
        if (RevokedAt.HasValue) return;
        if (expectedVersion != Version) throw new DomainException("O convite foi alterado. Recarregue a versão atual.");
        if (actorId == Guid.Empty) throw new ArgumentException("Ator inválido.");
        RevokedAt = now; RevokedBy = actorId; Version++; MarkAsUpdated(actorId);
    }
}
