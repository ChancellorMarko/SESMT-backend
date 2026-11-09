using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Application.Modules.IdentityAccess.Services;

/// <summary>
/// Coordena a renovação e rotação de sessões.
/// A rotação é estrita, com uma exceção: o token imediatamente anterior é aceito uma
/// única vez dentro de <see cref="SessionPolicy.RefreshReuseGraceSeconds"/>, para quando o
/// navegador perde a resposta da rotação. Depois da janela, apresentá-lo indica reutilização
/// e revoga a sessão. Os dois casos são auditados.
/// </summary>
public sealed class RefreshSessionService : IRefreshSessionService
{
    private readonly IUserAuthenticationRepository _authenticationRepository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly TimeProvider _timeProvider;
    private readonly SessionPolicy _sessionPolicy;
    private readonly IAuditLogRepository? _auditRepository;
    private readonly ICurrentUserService? _currentUser;

    public const string GraceAction = "SESSAO_RENOVADA_TOLERANCIA";
    public const string ReuseAction = "SESSAO_REVOGADA_REUSO";

    public RefreshSessionService(
        IUserAuthenticationRepository authenticationRepository,
        IUserSessionRepository sessionRepository,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        TimeProvider? timeProvider = null,
        SessionPolicy? sessionPolicy = null,
        IAuditLogRepository? auditRepository = null,
        ICurrentUserService? currentUser = null)
    {
        _authenticationRepository = authenticationRepository;
        _sessionRepository = sessionRepository;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _sessionPolicy = sessionPolicy ?? new SessionPolicy();
        _auditRepository = auditRepository;
        _currentUser = currentUser;

        if (_sessionPolicy.RefreshTokenExpirationDays <= 0)
        {
            throw new ArgumentException(
                "A duração do refresh token deve ser positiva.",
                nameof(sessionPolicy));
        }

        if (_sessionPolicy.RefreshReuseGraceSeconds is < 0 or > 120)
        {
            throw new ArgumentException(
                "A janela de tolerância do refresh token deve estar entre 0 e 120 segundos.",
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

        var now = _timeProvider.GetUtcNow();
        var usingPreviousToken = false;

        if (session is null)
        {
            session = await _sessionRepository.GetByPreviousRefreshTokenHashAsync(
                currentRefreshTokenHash,
                cancellationToken);

            if (session is null)
            {
                return RefreshTokenResult.Failure("invalid_refresh_token");
            }

            if (!session.IsValid(now))
            {
                return RefreshTokenResult.Failure(
                    session.RevokedAt is not null
                        ? "session_revoked"
                        : "session_expired");
            }

            if (!session.IsWithinReuseGrace(
                    now,
                    TimeSpan.FromSeconds(_sessionPolicy.RefreshReuseGraceSeconds)))
            {
                // The replaced token came back after the grace window: treat as reuse.
                session.Revoke();
                await AuditAsync(ReuseAction, session, cancellationToken);
                await _sessionRepository.SaveChangesAsync(cancellationToken);
                return RefreshTokenResult.Failure("session_revoked");
            }

            usingPreviousToken = true;
        }

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

        // A grace rotation replaces the token lost by the browser, so the previous token
        // stops matching anything and cannot be accepted a second time.
        session.RotateRefreshToken(
            newRefreshTokenHash,
            newRefreshTokenExpiresAt,
            now);

        if (usingPreviousToken)
        {
            await AuditAsync(GraceAction, session, cancellationToken);
        }

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

    private async Task AuditAsync(
        string action,
        UserSession session,
        CancellationToken cancellationToken)
    {
        if (_auditRepository is null)
        {
            return;
        }

        var lastHash = await _auditRepository.GetLastHashAsync(cancellationToken);
        await _auditRepository.AddAsync(
            AuditLog.ForCustom(
                action,
                "sessao",
                session.Id,
                session.UserId,
                actorEmail: null,
                actorName: null,
                previousData: null,
                newData: null,
                context: null,
                previousHash: lastHash,
                ipAddress: _currentUser?.IpAddress,
                userAgent: _currentUser?.UserAgent,
                result: action == GraceAction ? "SUCESSO" : "BLOQUEADO",
                traceId: _currentUser?.TraceId),
            cancellationToken);
    }
}
