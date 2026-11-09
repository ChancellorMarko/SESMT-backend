using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;
using Microsoft.EntityFrameworkCore;

namespace SESMT.Tests.Integration.API;

/// <summary>Refresh reuse grace against the real session repository and PostgreSQL mapping.</summary>
[Collection(PostgresCollection.Name)]
public sealed class UserSessionReuseGracePostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task PreviousTokenIsFoundOnceWithinTheWindowAndRevokesAfterIt()
    {
        var key = Guid.NewGuid().ToString("N");
        Guid userId, sessionId;
        await using (var db = database.CreateContext())
        {
            var user = new User("Sessão fictícia", $"session-{key}@example.invalid", "fixture-only-hash");
            var session = new UserSession(user.Id, $"hash:t0-{key}", DateTimeOffset.UtcNow.AddDays(1));
            db.AddRange(user, session);
            await db.SaveChangesAsync();
            userId = user.Id; sessionId = session.Id;
        }

        var start = DateTimeOffset.UtcNow;
        var clock = new Clock(start);
        async Task<RefreshTokenResult> Refresh(string token, string next)
        {
            await using var db = database.CreateContext();
            var service = new RefreshSessionService(
                new SingleUser(await db.Users.SingleAsync(u => u.Id == userId)),
                new UserSessionRepository(db), new Jwt(), new Tokens(next), clock, new SessionPolicy(7, 30));
            return await service.ExecuteAsync(new RefreshTokenRequest(token));
        }

        Assert.True((await Refresh($"t0-{key}", $"t1-{key}")).Succeeded);   // response lost by the browser
        clock.Now = start.AddSeconds(10);
        Assert.True((await Refresh($"t0-{key}", $"t2-{key}")).Succeeded);   // grace: renewed once
        Assert.Equal("invalid_refresh_token", (await Refresh($"t0-{key}", $"t3-{key}")).ErrorCode);
        clock.Now = start.AddSeconds(60);
        Assert.True((await Refresh($"t2-{key}", $"t4-{key}")).Succeeded);   // normal rotation continues
        clock.Now = start.AddSeconds(120);
        Assert.Equal("session_revoked", (await Refresh($"t2-{key}", $"t5-{key}")).ErrorCode); // reuse after window

        await using var check = database.CreateContext();
        var stored = await check.UserSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal($"hash:t4-{key}", stored.RefreshTokenHash);
        Assert.Equal($"hash:t2-{key}", stored.PreviousRefreshTokenHash);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SingleUser(User user) : IUserAuthenticationRepository
    {
        public Task<UserAuthenticationData?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAuthenticationData?>(null);
        public Task<UserAuthenticationData?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAuthenticationData?>(userId == user.Id ? new UserAuthenticationData(user, [], []) : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Jwt : IJwtTokenService
    {
        public AccessTokenResult GenerateAccessToken(Guid userId, IEnumerable<string> roles, IEnumerable<UserScopeClaim> scopes) =>
            new("access-token-de-teste", DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private sealed class Tokens(string next) : IRefreshTokenService
    {
        public string GenerateToken() => next;
        public string HashToken(string refreshToken) => $"hash:{refreshToken}";
    }
}
