using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Unit.Identity;

public sealed class LoginServiceTests
{
    private const string Email = "usuario@sesmt.com";
    private const string Password = "SenhaDeTeste123!";

    private static User CreateUser() =>
        new("Usuário Teste", Email, "hash-de-teste");

    private static UserAuthenticationData CreateAuthenticationData(User user) =>
        new(
            user,
            ["SESMT_ADMIN"],
            [new UserScopeClaim(ScopeType.UNIDADE, Guid.NewGuid())]);

    private static LoginService CreateService(
        FakeUserAuthenticationRepository repository,
        FakePasswordHashService passwordService,
        FakeJwtTokenService jwtService,
        FakeUserSessionRepository? sessionRepository = null,
        FakeRefreshTokenService? refreshTokenService = null,
        FakeAuditLogRepository? auditRepository = null,
        LoginPolicy? policy = null)
    {
        return new LoginService(
            repository,
            sessionRepository ?? new FakeUserSessionRepository(),
            passwordService,
            jwtService,
            refreshTokenService ?? new FakeRefreshTokenService(),
            auditRepository ?? new FakeAuditLogRepository(),   // 6º: audit
            TimeProvider.System,                                // 7º: timeProvider
            policy);                                            // 8º: policy
    }

    // ────────────────────────────────────────────────────────────────────────
    // Sucesso
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ShouldReturnToken()
    {
        var user = CreateUser();
        var repository = new FakeUserAuthenticationRepository(CreateAuthenticationData(user));
        var sessionRepository = new FakeUserSessionRepository();
        var passwordService = new FakePasswordHashService(true);
        var jwtService = new FakeJwtTokenService();
        var refreshTokenService = new FakeRefreshTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            sessionRepository, refreshTokenService, auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest(Email, Password));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal("token-de-teste", result.Response.AccessToken);
        Assert.Equal("refresh-token-de-teste", result.Response.RefreshToken);
        Assert.True(result.Response.RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.Equal(user.Id, result.Response.UserId);
        Assert.Equal(0, user.LoginAttempts);
        Assert.NotNull(user.LastLogin);
        Assert.Equal(0, repository.SaveChangesCalls);
        Assert.Equal(1, sessionRepository.AddCalls);
        Assert.Equal(1, sessionRepository.SaveChangesCalls);
        Assert.NotNull(sessionRepository.AddedSession);
        Assert.Equal(user.Id, sessionRepository.AddedSession.UserId);
        Assert.Equal("hash:refresh-token-de-teste", sessionRepository.AddedSession.RefreshTokenHash);
        Assert.Equal(1, jwtService.GenerateCalls);
        Assert.Equal(1, refreshTokenService.GenerateCalls);

        // ─── Auditoria ────────────────────────────────────────────────────
        Assert.Equal(1, auditRepository.AddCalls);
        Assert.Equal(1, auditRepository.GetLastHashCalls);
        Assert.Equal(AuditAction.Login, auditRepository.LastAdded!.Action);
        Assert.Equal(AuditResource.User, auditRepository.LastAdded.ResourceType);
        Assert.Equal(user.Id, auditRepository.LastAdded.ResourceId);
        Assert.Equal(user.Id, auditRepository.LastAdded.ActorId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Senha inválida
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithInvalidPassword_ShouldRegisterFailedAttempt()
    {
        var user = CreateUser();
        var repository = new FakeUserAuthenticationRepository(CreateAuthenticationData(user));
        var passwordService = new FakePasswordHashService(false);
        var jwtService = new FakeJwtTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            auditRepository: auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest(Email, "senha-incorreta"));

        Assert.False(result.Succeeded);
        Assert.Null(result.Response);
        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.Equal(1, user.LoginAttempts);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Equal(0, jwtService.GenerateCalls);

        // ─── Auditoria ────────────────────────────────────────────────────
        Assert.Equal(1, auditRepository.AddCalls);
        Assert.Equal(AuditAction.LoginFailed, auditRepository.LastAdded!.Action);
        Assert.Equal(user.Id, auditRepository.LastAdded.ActorId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // E-mail não cadastrado (não audita — evita spam)
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithUnknownEmail_ShouldReturnGenericError()
    {
        var repository = new FakeUserAuthenticationRepository(null);
        var passwordService = new FakePasswordHashService(false);
        var jwtService = new FakeJwtTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            auditRepository: auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest(Email, Password));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.Equal(0, repository.SaveChangesCalls);
        Assert.Equal(0, jwtService.GenerateCalls);

        // Sem usuário → sem auditoria (evita enumeration e spam)
        Assert.Equal(0, auditRepository.AddCalls);
        Assert.Equal(0, auditRepository.GetLastHashCalls);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Usuário inativo
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithInactiveUser_ShouldRejectLogin()
    {
        var user = CreateUser();
        user.MarkAsDeleted();

        var repository = new FakeUserAuthenticationRepository(CreateAuthenticationData(user));
        var passwordService = new FakePasswordHashService(true);
        var jwtService = new FakeJwtTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            auditRepository: auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest(Email, Password));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.Equal(0, jwtService.GenerateCalls);
        Assert.Equal(0, passwordService.VerifyCalls);

        // Retorno antecipado → sem auditoria
        Assert.Equal(0, auditRepository.AddCalls);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Usuário bloqueado
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithBlockedUser_ShouldRejectLogin()
    {
        var user = CreateUser();
        user.RegisterFailedLogin(1, TimeSpan.FromMinutes(15));

        var repository = new FakeUserAuthenticationRepository(CreateAuthenticationData(user));
        var passwordService = new FakePasswordHashService(true);
        var jwtService = new FakeJwtTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            auditRepository: auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest(Email, Password));

        Assert.False(result.Succeeded);
        Assert.Equal("account_blocked", result.ErrorCode);
        Assert.Equal(0, passwordService.VerifyCalls);
        Assert.Equal(0, jwtService.GenerateCalls);
        Assert.Equal(0, auditRepository.AddCalls);
    }

    // ────────────────────────────────────────────────────────────────────────
    // MFA pendente
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithMfaEnabled_ShouldNotGenerateAccessToken()
    {
        var user = CreateUser();
        user.EnableMfa([1, 2, 3]);

        var repository = new FakeUserAuthenticationRepository(CreateAuthenticationData(user));
        var passwordService = new FakePasswordHashService(true);
        var jwtService = new FakeJwtTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            auditRepository: auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest(Email, Password));

        Assert.False(result.Succeeded);
        Assert.Null(result.Response);
        Assert.Equal("mfa_required", result.ErrorCode);
        Assert.Equal(0, jwtService.GenerateCalls);
        Assert.Equal(0, repository.SaveChangesCalls);
        Assert.Equal(0, auditRepository.AddCalls);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Credenciais vazias
    // ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WithEmptyCredentials_ShouldRejectWithoutQuery()
    {
        var repository = new FakeUserAuthenticationRepository(null);
        var passwordService = new FakePasswordHashService(false);
        var jwtService = new FakeJwtTokenService();
        var auditRepository = new FakeAuditLogRepository();

        var service = CreateService(
            repository, passwordService, jwtService,
            auditRepository: auditRepository);

        var result = await service.ExecuteAsync(new LoginRequest("", ""));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_credentials", result.ErrorCode);
        Assert.Equal(0, repository.GetByEmailCalls);
        Assert.Equal(0, jwtService.GenerateCalls);
        Assert.Equal(0, auditRepository.AddCalls);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Fakes
    // ════════════════════════════════════════════════════════════════════════

    private sealed class FakeUserAuthenticationRepository : IUserAuthenticationRepository
    {
        private readonly UserAuthenticationData? _data;

        public int GetByEmailCalls { get; private set; }
        public int SaveChangesCalls { get; private set; }

        public FakeUserAuthenticationRepository(UserAuthenticationData? data)
        {
            _data = data;
        }

        public Task<UserAuthenticationData?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            GetByEmailCalls++;
            return Task.FromResult(_data);
        }

        public Task<UserAuthenticationData?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_data?.User.Id == userId ? _data : null);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserSessionRepository : IUserSessionRepository
    {
        public int AddCalls { get; private set; }
        public int SaveChangesCalls { get; private set; }
        public UserSession? AddedSession { get; private set; }

        public Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            AddedSession = session;
            return Task.CompletedTask;
        }

        public Task<UserSession?> GetByRefreshTokenHashAsync(
            string refreshTokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserSession?>(null);

        public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        private readonly bool _isValid;

        public int VerifyCalls { get; private set; }

        public FakePasswordHashService(bool isValid) => _isValid = isValid;

        public string HashPassword(string password) => $"fake-hash:{password}";

        public bool VerifyPassword(string passwordHash, string providedPassword)
        {
            VerifyCalls++;
            return _isValid;
        }
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
                "token-de-teste",
                DateTimeOffset.UtcNow.AddMinutes(15));   // ← DateTimeOffset
        }
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public int GenerateCalls { get; private set; }

        public string GenerateToken()
        {
            GenerateCalls++;
            return "refresh-token-de-teste";
        }

        public string HashToken(string refreshToken) => $"hash:{refreshToken}";
    }

    /// <summary>
    /// Fake que rastreia chamadas ao repositório de auditoria para permitir
    /// asserções sobre geração de LOGIN e LOGIN_FALHOU.
    /// </summary>
    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public int AddCalls { get; private set; }
        public int GetLastHashCalls { get; private set; }
        public AuditLog? LastAdded { get; private set; }

        public Task AddAsync(AuditLog log, CancellationToken ct = default)
        {
            AddCalls++;
            LastAdded = log;
            return Task.CompletedTask;
        }

        public Task<string?> GetLastHashAsync(CancellationToken ct = default)
        {
            GetLastHashCalls++;
            return Task.FromResult<string?>(null);
        }

        public Task<IReadOnlyList<AuditLog>> QueryAsync(
            Guid? actorId, string? action, string? resourceType, Guid? resourceId,
            DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<Guid> companyIds,
            int skip, int take,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());

        public Task<int> CountAsync(
            Guid? actorId, string? action, string? resourceType, Guid? resourceId,
            DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<Guid> companyIds,
            CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyCollection<Guid>> GetAccessibleCompanyIdsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());

        public Task<AuditLog?> GetByEventIdAsync(Guid eventId, IReadOnlyCollection<Guid> companyIds, CancellationToken ct = default) =>
            Task.FromResult<AuditLog?>(null);
    }
}
