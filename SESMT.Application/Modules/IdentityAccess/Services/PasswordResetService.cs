using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Application.Modules.IdentityAccess.Services;

public sealed class PasswordResetService : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    private readonly IPasswordResetRepository _resetRepository;
    private readonly IUserSessionRepository _sessionRepository;
    private readonly IPasswordResetNotificationService _notificationService;
    private readonly IRefreshTokenService _tokenService;
    private readonly IPasswordHashService _passwordHashService;
    private readonly TimeProvider _timeProvider;

    public PasswordResetService(
        IPasswordResetRepository resetRepository,
        IUserSessionRepository sessionRepository,
        IPasswordResetNotificationService notificationService,
        IRefreshTokenService tokenService,
        IPasswordHashService passwordHashService,
        TimeProvider? timeProvider = null)
    {
        _resetRepository = resetRepository;
        _sessionRepository = sessionRepository;
        _notificationService = notificationService;
        _tokenService = tokenService;
        _passwordHashService = passwordHashService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task RequestAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return;

        var user = await _resetRepository.GetActiveUserByEmailAsync(
            request.Email,
            cancellationToken);
        if (user is null || !user.Active)
            return;

        var token = _tokenService.GenerateToken();
        var expiresAt = _timeProvider.GetUtcNow().Add(TokenLifetime);
        await _resetRepository.AddAsync(
            new PasswordResetToken(user.Id, _tokenService.HashToken(token), expiresAt),
            cancellationToken);
        await _resetRepository.SaveChangesAsync(cancellationToken);
        await _notificationService.SendAsync(user.Email, token, cancellationToken);
    }

    public async Task<PasswordResetResult> ResetAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword)
            || request.NewPassword.Length < 12)
            return PasswordResetResult.Failure("invalid_or_expired_token");

        var tokenHash = _tokenService.HashToken(request.Token);
        var resetToken = await _resetRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (resetToken is null || !resetToken.IsValid(now))
            return PasswordResetResult.Failure("invalid_or_expired_token");

        var user = await _resetRepository.GetUserByIdAsync(
            resetToken.UserId,
            cancellationToken);
        if (user is null || !user.Active || !resetToken.MarkUsed(now))
            return PasswordResetResult.Failure("invalid_or_expired_token");

        user.UpdatePassword(_passwordHashService.HashPassword(request.NewPassword));
        await _sessionRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _resetRepository.SaveChangesAsync(cancellationToken);
        return PasswordResetResult.Success();
    }
}
