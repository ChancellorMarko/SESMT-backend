using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.Application.Modules.IdentityAccess.Services;

/// <summary>
/// Coordena a renovação e rotação de sessões.
/// </summary>
public sealed class RefreshSessionService : IRefreshSessionService
{
    private readonly IUserAuthenticationRepository _authenticationRepository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly TimeProvider _timeProvider;
    private readonly SessionPolicy _sessionPolicy;

    public RefreshSessionService(
        IUserAuthenticationRepository authenticationRepository,
        IUserSessionRepository sessionRepository,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        TimeProvider? timeProvider = null,
        SessionPolicy? sessionPolicy = null)
    {
        _authenticationRepository = authenticationRepository;
        _sessionRepository = sessionRepository;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _sessionPolicy = sessionPolicy ?? new SessionPolicy();

        if (_sessionPolicy.RefreshTokenExpirationDays <= 0)
        {
            throw new ArgumentException(
                "A duração do refresh token deve ser positiva.",
                nameof(sessionPolicy));
        }
    }

    public async Task<RefreshTokenResult> ExecuteAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return RefreshTokenResult.Failure("invalid_refresh_token");
        }

        var currentRefreshTokenHash = _refreshTokenService.HashToken(
            request.RefreshToken);

        var session = await _sessionRepository.GetByRefreshTokenHashAsync(
            currentRefreshTokenHash,
            cancellationToken);

        if (session is null)
        {
            return RefreshTokenResult.Failure("invalid_refresh_token");
        }

        var now = _timeProvider.GetUtcNow();

        if (!session.IsValid(now))
        {
            return RefreshTokenResult.Failure(
                session.RevokedAt is not null
                    ? "session_revoked"
                    : "session_expired");
        }

        var authenticationData = await _authenticationRepository.GetByIdAsync(
            session.UserId,
            cancellationToken);

        if (authenticationData is null
            || !authenticationData.User.Active)
        {
            session.Revoke();
            await _sessionRepository.SaveChangesAsync(cancellationToken);

            return RefreshTokenResult.Failure("invalid_refresh_token");
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(
            authenticationData.User.Id,
            authenticationData.Roles,
            authenticationData.Scopes);

        var newRefreshToken = _refreshTokenService.GenerateToken();
        var newRefreshTokenHash = _refreshTokenService.HashToken(
            newRefreshToken);
        var newRefreshTokenExpiresAt = now.AddDays(
            _sessionPolicy.RefreshTokenExpirationDays);

        session.RotateRefreshToken(
            newRefreshTokenHash,
            newRefreshTokenExpiresAt);

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            newRefreshToken,
            newRefreshTokenExpiresAt,
            authenticationData.User.Id,
            authenticationData.Roles,
            authenticationData.Scopes);

        return RefreshTokenResult.Success(response);
    }
}
