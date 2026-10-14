using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Application.Modules.IdentityAccess.Services;

/// <summary>
/// Coordena a autenticação de usuários.
/// </summary>
public sealed class LoginService : ILoginService
{
    private readonly IUserAuthenticationRepository _repository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly TimeProvider _timeProvider;
    private readonly LoginPolicy _policy;
    private readonly SessionPolicy _sessionPolicy;

    public LoginService(
        IUserAuthenticationRepository repository,
        IUserSessionRepository sessionRepository,
        IPasswordHashService passwordHashService,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        TimeProvider? timeProvider = null,
        LoginPolicy? policy = null,
        SessionPolicy? sessionPolicy = null)
    {
        _repository = repository;
        _sessionRepository = sessionRepository;
        _passwordHashService = passwordHashService;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _policy = policy ?? new LoginPolicy();
        _sessionPolicy = sessionPolicy ?? new SessionPolicy();

        if (_policy.MaxAttempts <= 0
            || _policy.BlockDurationMinutes <= 0)
        {
            throw new ArgumentException(
                "A política de login deve possuir limites positivos.",
                nameof(policy));
        }

        if (_sessionPolicy.RefreshTokenExpirationDays <= 0)
        {
            throw new ArgumentException(
                "A duração do refresh token deve ser positiva.",
                nameof(sessionPolicy));
        }
    }

    public async Task<LoginResult> ExecuteAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return LoginResult.Failure("invalid_credentials");
        }

        var authenticationData = await _repository.GetByEmailAsync(
            request.Email,
            cancellationToken);

        if (authenticationData is null)
        {
            return LoginResult.Failure("invalid_credentials");
        }

        var user = authenticationData.User;

        if (!user.Active)
        {
            return LoginResult.Failure("invalid_credentials");
        }

        var now = DateTimeOffset.UtcNow;

        if (user.BlockedUntil.HasValue
            && user.BlockedUntil.Value > now)
        {
            return LoginResult.Failure("account_blocked");
        }

        var passwordIsValid = _passwordHashService.VerifyPassword(
            user.PasswordHash,
            request.Password);

        if (!passwordIsValid)
        {
            user.RegisterFailedLogin(
                _policy.MaxAttempts,
                TimeSpan.FromMinutes(_policy.BlockDurationMinutes));

            await _repository.SaveChangesAsync(cancellationToken);

            return LoginResult.Failure("invalid_credentials");
        }

        if (user.MfaEnabled)
        {
            return LoginResult.Failure("mfa_required");
        }

        user.RegisterLogin();

        var tokenResult = _jwtTokenService.GenerateAccessToken(
            user.Id,
            authenticationData.Roles,
            authenticationData.Scopes);

        var refreshToken = _refreshTokenService.GenerateToken();
        var refreshTokenHash = _refreshTokenService.HashToken(refreshToken);
        var refreshTokenExpiresAt = _timeProvider
            .GetUtcNow()
            .AddDays(_sessionPolicy.RefreshTokenExpirationDays);

        var session = new UserSession(
            user.Id,
            refreshTokenHash,
            refreshTokenExpiresAt);

        await _sessionRepository.AddAsync(
            session,
            cancellationToken);

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(
            tokenResult.Token,
            tokenResult.ExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAt,
            user.Id,
            authenticationData.Roles,
            authenticationData.Scopes);

        return LoginResult.Success(response);
    }
}
