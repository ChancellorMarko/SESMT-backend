using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public class UserSession : AggregateRootBase
{
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    protected UserSession() { }

    public UserSession(
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "O ID do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(refreshTokenHash))
            throw new ArgumentException(
                "O hash do refresh token é obrigatório.");

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException(
                "A expiração deve ser futura.");

        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsValid(DateTimeOffset now)
    {
        return Active
            && RevokedAt is null
            && ExpiresAt > now;
    }

    public void RotateRefreshToken(
        string refreshTokenHash,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenHash))
            throw new ArgumentException(
                "O hash do refresh token é obrigatório.");

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException(
                "A expiração deve ser futura.");

        RefreshTokenHash = refreshTokenHash;
        ExpiresAt = expiresAt;
        MarkAsUpdated();
    }

    public void Revoke()
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTimeOffset.UtcNow;
        Active = false;
        MarkAsUpdated();
    }
}
