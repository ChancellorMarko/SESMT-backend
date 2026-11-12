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
	public async Task RefreshAsync_WhenAnotherRefreshRotatedFirst_ShouldReportConflictWithoutRevoking()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(
			user.Id,
			$"hash:{OldRefreshToken}",
			now.AddDays(1));

		var sessionRepository = new FakeUserSessionRepository(session)
		{
			ConflictOnRotation = true,
		};

		var service = new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(user)),
			sessionRepository,
			new FakeJwtTokenService(),
			new FakeRefreshTokenService(NewRefreshToken),
			new FakeTimeProvider(now),
			new SessionPolicy(7));

		var result = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));

		Assert.False(result.Succeeded);
		Assert.Null(result.Response);
		Assert.Equal(RefreshTokenResult.ConflictError, result.ErrorCode);
		Assert.Null(session.RevokedAt);
		Assert.Equal(0, sessionRepository.SaveChangesCalls);
	}

	[Fact]
	public async Task RefreshAsync_WithReusedRefreshToken_ShouldRejectSecondReuseAfterGrace()
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
		var graceResult = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));
		var thirdResult = await service.ExecuteAsync(
			new RefreshTokenRequest(OldRefreshToken));

		Assert.True(firstResult.Succeeded);
		Assert.True(graceResult.Succeeded);
		Assert.False(thirdResult.Succeeded);
		Assert.Equal("invalid_refresh_token", thirdResult.ErrorCode);
		Assert.Equal(2, jwtTokenService.GenerateCalls);
		Assert.Equal(2, sessionRepository.SaveChangesCalls);
	}

	[Fact]
	public async Task RefreshAsync_WithPreviousTokenInsideGraceWindow_ShouldRenewOnlyOnce()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(user.Id, $"hash:{OldRefreshToken}", now.AddDays(1));
		var sessionRepository = new FakeUserSessionRepository(session);
		var refreshTokenService = new SequenceRefreshTokenService("refresh-perdido", "refresh-recuperado");
		var clock = new FakeTimeProvider(now);
		var audit = new FakeAuditLogRepository();
		var service = new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(user)),
			sessionRepository, new FakeJwtTokenService(), refreshTokenService, clock, new SessionPolicy(7), audit);

		// The browser loses the response of this rotation and keeps the old token.
		var lost = await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));
		clock.Advance(TimeSpan.FromSeconds(20));
		var recovered = await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));
		var replay = await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));

		Assert.True(lost.Succeeded);
		Assert.True(recovered.Succeeded);
		Assert.Equal("refresh-recuperado", recovered.Response!.RefreshToken);
		Assert.Equal("hash:refresh-recuperado", session.RefreshTokenHash);
		Assert.Equal("hash:refresh-perdido", session.PreviousRefreshTokenHash);
		Assert.False(replay.Succeeded);
		Assert.Equal("invalid_refresh_token", replay.ErrorCode);
		Assert.True(session.IsValid(clock.GetUtcNow()));
		Assert.Equal([RefreshSessionService.GraceAction], audit.Actions);
	}

	[Fact]
	public async Task RefreshAsync_WithPreviousTokenAfterGraceWindow_ShouldRevokeSession()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(user.Id, $"hash:{OldRefreshToken}", now.AddDays(1));
		var clock = new FakeTimeProvider(now);
		var audit = new FakeAuditLogRepository();
		var service = new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(user)),
			new FakeUserSessionRepository(session), new FakeJwtTokenService(),
			new FakeRefreshTokenService(NewRefreshToken), clock, new SessionPolicy(7), audit);

		var rotated = await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));
		clock.Advance(TimeSpan.FromSeconds(31));
		var reused = await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));
		var current = await service.ExecuteAsync(new RefreshTokenRequest(NewRefreshToken));

		Assert.True(rotated.Succeeded);
		Assert.False(reused.Succeeded);
		Assert.Equal("session_revoked", reused.ErrorCode);
		Assert.NotNull(session.RevokedAt);
		Assert.False(current.Succeeded);
		Assert.Equal("session_revoked", current.ErrorCode);
		Assert.Equal([RefreshSessionService.ReuseAction], audit.Actions);
	}

	[Fact]
	public async Task RefreshAsync_WithZeroGraceWindow_ShouldKeepStrictRotation()
	{
		var now = DateTimeOffset.UtcNow;
		var user = CreateUser();
		var session = new UserSession(user.Id, $"hash:{OldRefreshToken}", now.AddDays(1));
		var service = new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(user)),
			new FakeUserSessionRepository(session), new FakeJwtTokenService(),
			new FakeRefreshTokenService(NewRefreshToken), new FakeTimeProvider(now), new SessionPolicy(7, 0));

		await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));
		var second = await service.ExecuteAsync(new RefreshTokenRequest(OldRefreshToken));

		// Without a window the replaced token is never accepted; presenting it revokes the session.
		Assert.False(second.Succeeded);
		Assert.Equal("session_revoked", second.ErrorCode);
		Assert.NotNull(session.RevokedAt);
	}

	[Fact]
	public void Constructor_WithGraceWindowOutsideLimits_ShouldThrow()
	{
		Assert.Throws<ArgumentException>(() => new RefreshSessionService(
			new FakeUserAuthenticationRepository(CreateAuthenticationData(CreateUser())),
			new FakeUserSessionRepository(null), new FakeJwtTokenService(),
			new FakeRefreshTokenService(NewRefreshToken), null, new SessionPolicy(7, 121)));
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

		public bool ConflictOnRotation { get; set; }

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

		public Task<UserSession?> GetByPreviousRefreshTokenHashAsync(
			string refreshTokenHash,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(
				_session?.PreviousRefreshTokenHash == refreshTokenHash
					? _session
					: null);
		}

		public Task SaveChangesAsync(
			CancellationToken cancellationToken = default)
		{
			SaveChangesCalls++;
			return Task.CompletedTask;
		}

		public Task<bool> TryPersistRotationAsync(
			UserSession session,
			string expectedRefreshTokenHash,
			CancellationToken cancellationToken = default)
		{
			if (ConflictOnRotation)
			{
				return Task.FromResult(false);
			}

			SaveChangesCalls++;
			return Task.FromResult(true);
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
		private DateTimeOffset _utcNow;

		public FakeTimeProvider(DateTimeOffset utcNow)
		{
			_utcNow = utcNow;
		}

		public void Advance(TimeSpan by) => _utcNow += by;

		public override DateTimeOffset GetUtcNow() =>
			_utcNow;
	}

	private sealed class SequenceRefreshTokenService(params string[] tokens) : IRefreshTokenService
	{
		private int _next;

		public string GenerateToken() => tokens[_next++];

		public string HashToken(string refreshToken) =>
			$"hash:{refreshToken}";
	}

	private sealed class FakeAuditLogRepository : SESMT.Domain.Modules.Audit.Repositories.IAuditLogRepository
	{
		public List<string> Actions { get; } = [];

		public Task AddAsync(SESMT.Domain.Modules.Audit.Entities.AuditLog log, CancellationToken ct = default)
		{
			Actions.Add(log.Action);
			return Task.CompletedTask;
		}

		public Task<string?> GetLastHashAsync(CancellationToken ct = default) =>
			Task.FromResult<string?>(null);

		public Task<IReadOnlyList<SESMT.Domain.Modules.Audit.Entities.AuditLog>> QueryAsync(
			Guid? actorId, string? action, string? resourceType, Guid? resourceId,
			DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<Guid> companyIds,
			int skip, int take, CancellationToken ct = default) =>
			Task.FromResult<IReadOnlyList<SESMT.Domain.Modules.Audit.Entities.AuditLog>>([]);

		public Task<int> CountAsync(
			Guid? actorId, string? action, string? resourceType, Guid? resourceId,
			DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<Guid> companyIds,
			CancellationToken ct = default) => Task.FromResult(0);

		public Task<IReadOnlyCollection<Guid>> GetAccessibleCompanyIdsAsync(Guid userId, CancellationToken ct = default) =>
			Task.FromResult<IReadOnlyCollection<Guid>>([]);

		public Task<SESMT.Domain.Modules.Audit.Entities.AuditLog?> GetByEventIdAsync(
			Guid eventId, IReadOnlyCollection<Guid> companyIds, CancellationToken ct = default) =>
			Task.FromResult<SESMT.Domain.Modules.Audit.Entities.AuditLog?>(null);
	}
}
