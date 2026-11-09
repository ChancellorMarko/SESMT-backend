using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public class UserSession : AggregateRootBase
{
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Hash of the refresh token replaced by the last rotation.</summary>
    public string? PreviousRefreshTokenHash { get; private set; }

    /// <summary>When the last rotation happened; bounds the reuse grace window.</summary>
    public DateTimeOffset? PreviousRotatedAt { get; private set; }

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
        DateTimeOffset expiresAt,
        DateTimeOffset? rotatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenHash))
            throw new ArgumentException(
                "O hash do refresh token é obrigatório.");

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException(
                "A expiração deve ser futura.");

        PreviousRefreshTokenHash = RefreshTokenHash;
        PreviousRotatedAt = rotatedAt ?? DateTimeOffset.UtcNow;
        RefreshTokenHash = refreshTokenHash;
        ExpiresAt = expiresAt;
        MarkAsUpdated();
    }

    /// <summary>True when the previous token is presented within the grace window.</summary>
    public bool IsWithinReuseGrace(DateTimeOffset now, TimeSpan grace) =>
        grace > TimeSpan.Zero && PreviousRotatedAt is { } rotatedAt && now >= rotatedAt && now - rotatedAt <= grace;

    public void Revoke()
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTimeOffset.UtcNow;
        Active = false;
        MarkAsUpdated();
    }
}
