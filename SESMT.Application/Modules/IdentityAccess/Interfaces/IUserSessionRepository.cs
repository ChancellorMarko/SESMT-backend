using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

/// <summary>
/// Define as operações de persistência para sessões de usuário.
/// </summary>
public interface IUserSessionRepository
{
    /// <summary>
    /// Persiste uma nova sessão com o hash do refresh token.
    /// </summary>
    Task AddAsync(
        UserSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca uma sessão pelo hash do refresh token.
    /// </summary>
    Task<UserSession?> GetByRefreshTokenHashAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste alterações de sessão, como rotação ou revogação.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
