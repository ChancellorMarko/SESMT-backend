using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public sealed class PasswordResetToken : AggregateRootBase
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    private PasswordResetToken() { }

    public PasswordResetToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("O ID do usuário é obrigatório.");
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A expiração deve ser futura.");

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsValid(DateTimeOffset now) =>
        Active && DeletedAt is null && UsedAt is null && ExpiresAt > now;

    public bool MarkUsed(DateTimeOffset now)
    {
        if (!IsValid(now))
            return false;

        UsedAt = now;
        MarkAsUpdated();
        return true;
    }
}
