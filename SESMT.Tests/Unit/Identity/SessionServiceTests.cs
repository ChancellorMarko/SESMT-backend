using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Unit.Identity;

public sealed class SessionServiceTests
{
	private const string OldRefreshToken = "refresh-token-antigo";
	private const string NewRefreshToken = "refresh-token-novo";

	private static User CreateUser() =>
		new("Usuário Teste", "usuario@sesmt.com", "hash-de-teste");

	private static UserAuthenticationData CreateAuthenticationData(
		User user)
	{
		return new UserAuthenticationData(
			user,
			["SESMT_ADMIN"],
			[new UserScopeClaim(ScopeType.UNIDADE, Guid.NewGuid())]);
	}

	[Fact]
	public async Task RefreshAsync_WithValidRefreshToken_ShouldRotateSession()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(
			user.Id,
			$"hash:{OldRefreshToken}",
			now.AddDays(1));

		var authenticationRepository = new FakeUserAuthenticationRepository(
			CreateAuthenticationData(user));
		var sessionRepository = new FakeUserSessionRepository(session);
		var jwtTokenService = new FakeJwtTokenService();
		var refreshTokenService = new FakeRefreshTokenService(NewRefreshToken);

		var service = new RefreshSessionService(
			authenticationRepository,
			sessionRepository,
			jwtTokenService,
			refreshTokenService,
			new FakeTimeProvider(now),
			new SessionPolicy(7));

		var result = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));

		Assert.True(result.Succeeded);
		Assert.NotNull(result.Response);
		Assert.Equal(NewRefreshToken, result.Response.RefreshToken);
		Assert.Equal($"hash:{NewRefreshToken}", session.RefreshTokenHash);
		Assert.Equal(now.AddDays(7), session.ExpiresAt);
		Assert.Equal(1, jwtTokenService.GenerateCalls);
		Assert.Equal(1, refreshTokenService.GenerateCalls);
		Assert.Equal(1, sessionRepository.SaveChangesCalls);
	}

	[Fact]
	public async Task RefreshAsync_WithReusedRefreshToken_ShouldRejectAfterRotation()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(
			user.Id,
			$"hash:{OldRefreshToken}",
			now.AddDays(1));

		var authenticationRepository = new FakeUserAuthenticationRepository(
			CreateAuthenticationData(user));
		var sessionRepository = new FakeUserSessionRepository(session);
		var jwtTokenService = new FakeJwtTokenService();
		var refreshTokenService = new FakeRefreshTokenService(NewRefreshToken);

		var service = new RefreshSessionService(
			authenticationRepository,
			sessionRepository,
			jwtTokenService,
			refreshTokenService,
			new FakeTimeProvider(now),
			new SessionPolicy(7));

		var firstResult = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));
		var secondResult = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));

		Assert.True(firstResult.Succeeded);
		Assert.False(secondResult.Succeeded);
		Assert.Equal("invalid_refresh_token", secondResult.ErrorCode);
		Assert.Equal(1, jwtTokenService.GenerateCalls);
		Assert.Equal(1, sessionRepository.SaveChangesCalls);
	}

	[Fact]
	public async Task RefreshAsync_WithExpiredSession_ShouldReject()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(
			user.Id,
			$"hash:{OldRefreshToken}",
			now.AddMinutes(1));

		var service = new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(user)),
			new FakeUserSessionRepository(session),
			new FakeJwtTokenService(),
			new FakeRefreshTokenService(NewRefreshToken),
			new FakeTimeProvider(now.AddMinutes(2)),
			new SessionPolicy(7));

		var result = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));

		Assert.False(result.Succeeded);
		Assert.Equal("session_expired", result.ErrorCode);
	}

	[Fact]
	public async Task RefreshAsync_WithRevokedSession_ShouldReject()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(
			user.Id,
			$"hash:{OldRefreshToken}",
			now.AddDays(1));
		session.Revoke();

		var service = new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(user)),
			new FakeUserSessionRepository(session),
			new FakeJwtTokenService(),
			new FakeRefreshTokenService(NewRefreshToken),
			new FakeTimeProvider(now),
			new SessionPolicy(7));

		var result = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));

		Assert.False(result.Succeeded);
		Assert.Equal("session_revoked", result.ErrorCode);
	}

	[Fact]
	public async Task LogoutAsync_WithValidRefreshToken_ShouldRevokeSession()
	{
		var user = CreateUser();
		var session = new UserSession(
			user.Id,
			$"hash:{OldRefreshToken}",
			DateTimeOffset.UtcNow.AddDays(1));

		var sessionRepository = new FakeUserSessionRepository(session);

		var service = new LogoutService(
			sessionRepository,
			new FakeRefreshTokenService(NewRefreshToken));

		var result = await service.ExecuteAsync(
			new LogoutRequest(OldRefreshToken));

		Assert.True(result.Succeeded);
		Assert.False(session.Active);
		Assert.NotNull(session.RevokedAt);
		Assert.Equal(1, sessionRepository.SaveChangesCalls);
	}

	private sealed class FakeUserAuthenticationRepository
		: IUserAuthenticationRepository
	{
		private readonly UserAuthenticationData? _data;

		public FakeUserAuthenticationRepository(
			UserAuthenticationData? data)
		{
			_data = data;
		}

		public Task<UserAuthenticationData?> GetByEmailAsync(
			string email,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_data);
		}

		public Task<UserAuthenticationData?> GetByIdAsync(
			Guid userId,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(
				_data?.User.Id == userId
					? _data
					: null);
		}

		public Task SaveChangesAsync(
			CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class FakeUserSessionRepository
		: IUserSessionRepository
	{
		private readonly UserSession? _session;

		public int SaveChangesCalls { get; private set; }

		public FakeUserSessionRepository(UserSession? session)
		{
			_session = session;
		}

		public Task AddAsync(
			UserSession session,
			CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task<UserSession?> GetByRefreshTokenHashAsync(
			string refreshTokenHash,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(
				_session?.RefreshTokenHash == refreshTokenHash
					? _session
					: null);
		}

		public Task SaveChangesAsync(
			CancellationToken cancellationToken = default)
		{
			SaveChangesCalls++;
			return Task.CompletedTask;
		}

		public Task RevokeAllForUserAsync(
			Guid userId,
			CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class FakeJwtTokenService : IJwtTokenService
	{
		public int GenerateCalls { get; private set; }

		public AccessTokenResult GenerateAccessToken(
			Guid userId,
			IEnumerable<string> roles,
			IEnumerable<UserScopeClaim> scopes)
		{
			GenerateCalls++;

			return new AccessTokenResult(
				"access-token-de-teste",
				DateTimeOffset.UtcNow.AddMinutes(15));
		}
	}

	private sealed class FakeRefreshTokenService : IRefreshTokenService
	{
		private readonly string _nextToken;

		public int GenerateCalls { get; private set; }

		public FakeRefreshTokenService(string nextToken)
		{
			_nextToken = nextToken;
		}

		public string GenerateToken()
		{
			GenerateCalls++;
			return _nextToken;
		}

		public string HashToken(string refreshToken) =>
			$"hash:{refreshToken}";
	}

	private sealed class FakeTimeProvider : TimeProvider
	{
		private readonly DateTimeOffset _utcNow;

		public FakeTimeProvider(DateTimeOffset utcNow)
		{
			_utcNow = utcNow;
		}

		public override DateTimeOffset GetUtcNow() =>
			_utcNow;
	}
}
