using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.Application.Modules.IdentityAccess.Services;

/// <summary>
/// Coordena a revogação de sessões de usuário.
/// </summary>
public sealed class LogoutService : ILogoutService
{
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutService(
        IUserSessionRepository sessionRepository,
        IRefreshTokenService refreshTokenService)
    {
        _sessionRepository = sessionRepository;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<LogoutResult> ExecuteAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return LogoutResult.Failure("invalid_refresh_token");
        }

        var refreshTokenHash = _refreshTokenService.HashToken(
            request.RefreshToken);

        var session = await _sessionRepository.GetByRefreshTokenHashAsync(
            refreshTokenHash,
            cancellationToken);

        if (session is null)
        {
            return LogoutResult.Success();
        }

        session.Revoke();

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return LogoutResult.Success();
    }
}
