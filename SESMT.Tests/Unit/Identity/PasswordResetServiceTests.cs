using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Unit.Identity;

public sealed class PasswordResetServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ResetAsync_WithValidToken_UpdatesPasswordAndRevokesSessionsOnce()
    {
        var user = new User("Usuário", "user@example.com", "old-hash");
        var token = new PasswordResetToken(user.Id, "hash:reset-token", Now.AddMinutes(30));
        var repository = new FakeResetRepository(user, token);
        var sessions = new FakeSessionRepository();
        var service = CreateService(repository, sessions);

        var result = await service.ResetAsync(new("reset-token", "NovaSenhaSegura123"));

        Assert.True(result.Succeeded);
        Assert.Equal("hashed:NovaSenhaSegura123", user.PasswordHash);
        Assert.NotNull(token.UsedAt);
        Assert.Equal(user.Id, sessions.RevokedUserId);
        Assert.Equal(1, repository.SaveCalls);

        var replay = await service.ResetAsync(new("reset-token", "OutraSenhaSegura123"));
        Assert.False(replay.Succeeded);
        Assert.Equal("hashed:NovaSenhaSegura123", user.PasswordHash);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task ResetAsync_WithExpiredToken_DoesNotChangePassword()
    {
        var user = new User("Usuário", "user@example.com", "old-hash");
        var token = new PasswordResetToken(user.Id, "hash:reset-token", Now.AddMinutes(30));
        var repository = new FakeResetRepository(user, token);
        var sessions = new FakeSessionRepository();

        var result = await CreateService(repository, sessions, now: Now.AddMinutes(31))
            .ResetAsync(new("reset-token", "NovaSenhaSegura123"));

        Assert.False(result.Succeeded);
        Assert.Equal("old-hash", user.PasswordHash);
        Assert.Null(token.UsedAt);
        Assert.Null(sessions.RevokedUserId);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task RequestAsync_WithUnknownEmail_DoesNotSendNotification()
    {
        var repository = new FakeResetRepository(null, null);
        var notification = new FakeNotificationService();

        await CreateService(repository, new FakeSessionRepository(), notification)
            .RequestAsync(new("unknown@example.com"));

        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, notification.SendCalls);
    }

    [Fact]
    public async Task RequestAsync_WithKnownEmail_PersistsHashAndSendsToken()
    {
        var user = new User("Usuário", "user@example.com", "old-hash");
        var repository = new FakeResetRepository(user, null);
        var notification = new FakeNotificationService();

        await CreateService(repository, new FakeSessionRepository(), notification)
            .RequestAsync(new(user.Email));

        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal("hash:reset-token", repository.AddedToken!.TokenHash);
        Assert.Equal("reset-token", notification.Token);
        Assert.Equal(user.Email, notification.Email);
    }

    private static PasswordResetService CreateService(
        FakeResetRepository repository,
        FakeSessionRepository sessions,
        FakeNotificationService? notification = null,
        DateTimeOffset? now = null) => new(
            repository,
            sessions,
            notification ?? new FakeNotificationService(),
            new FakeTokenService(),
            new FakePasswordHashService(),
            new FixedTimeProvider(now ?? Now));

    private sealed class FakeResetRepository(User? user, PasswordResetToken? token)
        : IPasswordResetRepository
    {
        public PasswordResetToken? AddedToken { get; private set; }
        public int SaveCalls { get; private set; }

        public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(user?.Email.Equals(email, StringComparison.OrdinalIgnoreCase) == true ? user : null);

        public Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(user?.Id == userId ? user : null);

        public Task AddAsync(PasswordResetToken resetToken, CancellationToken cancellationToken = default)
        {
            AddedToken = resetToken;
            token = resetToken;
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(token?.TokenHash == tokenHash ? token : null);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessionRepository : IUserSessionRepository
    {
        public Guid? RevokedUserId { get; private set; }
        public Task AddAsync(UserSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserSession?> GetByRefreshTokenHashAsync(string hash, CancellationToken cancellationToken = default) => Task.FromResult<UserSession?>(null);
        public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RevokedUserId = userId;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNotificationService : IPasswordResetNotificationService
    {
        public int SendCalls { get; private set; }
        public string? Email { get; private set; }
        public string? Token { get; private set; }
        public Task SendAsync(string email, string token, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            Email = email;
            Token = token;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenService : IRefreshTokenService
    {
        public string GenerateToken() => "reset-token";
        public string HashToken(string token) => $"hash:{token}";
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string passwordHash, string providedPassword) => false;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
