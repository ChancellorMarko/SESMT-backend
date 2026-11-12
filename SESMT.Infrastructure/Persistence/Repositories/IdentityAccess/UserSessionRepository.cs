using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;

/// <summary>
/// Implementa a persistência de sessões de usuário.
/// </summary>
public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly SesmtDbContext _dbContext;

    public UserSessionRepository(SesmtDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        UserSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _dbContext.UserSessions.AddAsync(
            session,
            cancellationToken);
    }

    public async Task<UserSession?> GetByRefreshTokenHashAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);

        return await _dbContext.UserSessions
            .FirstOrDefaultAsync(
                session => session.RefreshTokenHash == refreshTokenHash
                    && session.DeletedAt == null,
                cancellationToken);
    }

    public async Task<UserSession?> GetByPreviousRefreshTokenHashAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);

        return await _dbContext.UserSessions
            .FirstOrDefaultAsync(
                session => session.PreviousRefreshTokenHash == refreshTokenHash
                    && session.DeletedAt == null,
                cancellationToken);
    }

    public async Task<bool> TryPersistRotationAsync(
        UserSession session,
        string expectedRefreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRefreshTokenHash);

        // Conditional update: two concurrent refreshes with the same token cannot both win,
        // and a revocation committed meanwhile is not overwritten.
        var updated = await _dbContext.UserSessions
            .Where(stored => stored.Id == session.Id
                && stored.RefreshTokenHash == expectedRefreshTokenHash
                && stored.Active
                && stored.RevokedAt == null
                && stored.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(stored => stored.RefreshTokenHash, session.RefreshTokenHash)
                    .SetProperty(stored => stored.PreviousRefreshTokenHash, session.PreviousRefreshTokenHash)
                    .SetProperty(stored => stored.PreviousRotatedAt, session.PreviousRotatedAt)
                    .SetProperty(stored => stored.ExpiresAt, session.ExpiresAt)
                    .SetProperty(stored => stored.UpdatedAt, session.UpdatedAt),
                cancellationToken);

        // The tracked entity must not write the rotation again on a later SaveChanges.
        var entry = _dbContext.Entry(session);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }

        return updated == 1;
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserSessions
            .Where(session => session.UserId == userId
                && session.Active
                && session.RevokedAt == null
                && session.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.Revoke();
    }
}
