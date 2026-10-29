using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.API.Controllers;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Tests.Common;

namespace SESMT.Tests.Unit.Identity;

public sealed class AuthControllerTests
{
    private static readonly User ValidUser = new(
        "Fictional User",
        "user@example.com",
        "hash-do-teste");

    private static UserAuthenticationData AuthData() => new(
        ValidUser,
        ["SESMT_ADMIN"],
        [
            new UserScopeClaim(ScopeType.EMPRESA, Guid.NewGuid()),
            new UserScopeClaim(ScopeType.UNIDADE, Guid.NewGuid()),
            new UserScopeClaim(ScopeType.SETOR, Guid.NewGuid())
        ]);

    // ════════════════════════════════════════════════════════════════════════
    // ForgotPassword
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ForgotPassword_ReturnsSameAcceptedResponseForEveryEmail()
    {
        var controller = Build();

        var known = await controller.ForgotPassword(new("user@example.com"), CancellationToken.None);
        var unknown = await controller.ForgotPassword(new("unknown@example.com"), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(known);
        var unknownAccepted = Assert.IsType<AcceptedResult>(unknown);

        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Equal(StatusCodes.Status202Accepted, unknownAccepted.StatusCode);
        Assert.Equal(
            JsonSerializer.Serialize(accepted.Value),
            JsonSerializer.Serialize(unknownAccepted.Value));
    }

    // ════════════════════════════════════════════════════════════════════════
    // ResetPassword
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ResetPassword_WithValidToken_Returns204()
    {
        var controller = Build(resetResult: PasswordResetResult.Success());

        var result = await controller.ResetPassword(new("token", "NovaSenha@123"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400WithCode()
    {
        var controller = Build(resetResult: PasswordResetResult.Failure("invalid_or_expired_token"));

        var result = await controller.ResetPassword(new("token", "NovaSenha@123"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("INVALID_RESET_TOKEN", payload.Code);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Me
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Me_WithValidSession_ReturnsRichPayload()
    {
        var authData = AuthData();
        var controller = Build(authData: authData);
        AuthenticateAs(controller, authData.User.Id);

        var result = await controller.Me(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = doc.RootElement;

        Assert.Equal(authData.User.Id, root.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal(authData.User.Name, root.GetProperty("user").GetProperty("displayName").GetString());
        Assert.Equal(authData.User.Email.Value, root.GetProperty("user").GetProperty("email").GetString());

        Assert.Single(root.GetProperty("roles").EnumerateArray());
        Assert.Equal(0, root.GetProperty("permissions").GetArrayLength());

        Assert.Single(root.GetProperty("scope").GetProperty("companyIds").EnumerateArray());
        Assert.Single(root.GetProperty("scope").GetProperty("unitIds").EnumerateArray());
        Assert.Single(root.GetProperty("scope").GetProperty("sectorIds").EnumerateArray());
        Assert.Equal(0, root.GetProperty("scope").GetProperty("teamIds").GetArrayLength());
        Assert.False(root.GetProperty("scope").GetProperty("ownOnly").GetBoolean());

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessExpiresAt").GetString()));
    }

    [Fact]
    public async Task Me_WithInvalidClaims_Returns401()
    {
        var controller = Build();
        // Sem autenticar — HttpContext.User padrão não tem claim "sub"

        var result = await controller.Me(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
    }

    [Fact]
    public async Task Me_WhenUserNoLongerExists_Returns404()
    {
        var controller = Build(authData: null);
        AuthenticateAs(controller, Guid.NewGuid()); // sub válido, mas repo retorna null

        var result = await controller.Me(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Login
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Login_WithValidCredentials_SetsCookiesAndOmitsRefreshFromBody()
    {
        var loginResponse = new LoginResponse(
            "jwt-token", DateTimeOffset.UtcNow.AddMinutes(15),
            "refresh-token", DateTimeOffset.UtcNow.AddDays(7),
            ValidUser.Id, ["SESMT_ADMIN"], []);

        var controller = Build(loginResult: LoginResult.Success(loginResponse));

        var result = await controller.Login(new("user@example.com", "senha"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        // JSON não contém refreshToken
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain("refreshToken", json);

        // Cookies setados
        var setCookies = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("sesmt_refresh=refresh-token", setCookies);
        Assert.Contains("sesmt_csrf=", setCookies);
    }

    [Theory]
    [InlineData("invalid_credentials", 401, "INVALID_CREDENTIALS")]
    [InlineData("account_blocked", 401, "ACCOUNT_BLOCKED")]
    [InlineData("mfa_required", 403, "MFA_REQUIRED")]
    public async Task Login_WithError_MapsToCorrectStatusCode(string errorCode, int expectedStatus, string expectedCode)
    {
        var controller = Build(loginResult: LoginResult.Failure(errorCode));

        var result = await controller.Login(new("user@example.com", "senha"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal(expectedCode, payload.Code);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Csrf
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Csrf_ReturnsTokenAndSetsCookie()
    {
        var controller = Build();

        var result = controller.Csrf();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("csrfToken", json);

        var setCookies = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("sesmt_csrf=", setCookies);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Refresh
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Refresh_WithoutCsrfHeader_Returns403()
    {
        var controller = Build();
        WithRequestCookies(controller, ("sesmt_csrf", "server-token"));
        // Header ausente

        var result = await controller.Refresh(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("CSRF_INVALID", payload.Code);
    }

    [Fact]
    public async Task Refresh_WithoutRefreshCookie_Returns401()
    {
        var controller = Build();
        WithRequestCookies(controller, ("sesmt_csrf", "server-token"));
        WithRequestHeader(controller, "X-CSRF-Token", "server-token");

        var result = await controller.Refresh(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("NO_REFRESH_TOKEN", payload.Code);
    }

    [Fact]
    public async Task Refresh_WithValidSession_ReturnsNewTokensAndRotatesCookie()
    {
        var response = new LoginResponse(
            "new-jwt", DateTimeOffset.UtcNow.AddMinutes(15),
            "new-refresh", DateTimeOffset.UtcNow.AddDays(7),
            ValidUser.Id, ["SESMT_ADMIN"], []);

        var controller = Build(refreshResult: RefreshTokenResult.Success(response));
        WithRequestCookies(controller,
            ("sesmt_csrf", "server-token"),
            ("sesmt_refresh", "old-refresh"));
        WithRequestHeader(controller, "X-CSRF-Token", "server-token");

        var result = await controller.Refresh(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var setCookies = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("sesmt_refresh=new-refresh", setCookies);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Logout
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Logout_WithValidSession_Returns204AndClearsCookies()
    {
        var controller = Build(logoutResult: LogoutResult.Success());
        WithRequestCookies(controller, ("sesmt_refresh", "old-refresh"));

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        var setCookies = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("sesmt_refresh=", setCookies);
        // Deleção adiciona expires=Thu, 01 Jan 1970 ou Max-Age=0
        Assert.True(
            setCookies.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)
            || setCookies.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase),
            $"Expected cookie deletion header. Actual: {setCookies}");
    }

    [Fact]
    public async Task Logout_WhenRevocationFails_Returns500()
    {
        var controller = Build(logoutResult: LogoutResult.Failure("db-error"));
        WithRequestCookies(controller, ("sesmt_refresh", "old-refresh"));

        var result = await controller.Logout(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("LOGOUT_FAILED", payload.Code);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private static AuthController Build(
        LoginResult? loginResult = null,
        RefreshTokenResult? refreshResult = null,
        LogoutResult? logoutResult = null,
        PasswordResetResult? resetResult = null,
        UserAuthenticationData? authData = null)
    {
        var controller = new AuthController(
            new FakeLoginService(loginResult ?? LoginResult.Failure("invalid_credentials")),
            new FakeRefreshSessionService(refreshResult ?? RefreshTokenResult.Failure("invalid_refresh_token")),
            new FakeLogoutService(logoutResult ?? LogoutResult.Success()),
            new FakePasswordResetService(resetResult),
            new FakeUserAuthenticationRepository(authData));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    /// <summary>
    /// Popula HttpContext.User com um ClaimsPrincipal autenticado contendo os claims
    /// usados pelo AuthController: sub (NameIdentifier) e exp.
    /// </summary>
    private static void AuthenticateAs(AuthController controller, Guid userId, DateTimeOffset? expiresAt = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new("exp", (expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15))
                .ToUnixTimeSeconds().ToString()),
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, "TestAuth"));
    }

    /// <summary>
    /// Popula IRequestCookiesFeature para que Request.Cookies[name] retorne os valores.
    /// (Request.Cookies é read-only, então simulamos via feature.)
    /// </summary>
    private static void WithRequestCookies(
    AuthController controller,
    params (string Name, string Value)[] cookies)
    {
        var dict = cookies.ToDictionary(c => c.Name, c => c.Value);
        var collection = new FakeRequestCookies(dict); // ← Nossa classe fake
        controller.ControllerContext.HttpContext.Features
            .Set<IRequestCookiesFeature>(new RequestCookiesFeature(collection));
    }

    private static void WithRequestHeader(AuthController controller, string name, string value)
        => controller.Request.Headers[name] = value;

    // ════════════════════════════════════════════════════════════════════════
    // Fakes
    // ════════════════════════════════════════════════════════════════════════

    private sealed class FakeLoginService(LoginResult result) : ILoginService
    {
        public Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private sealed class FakeRefreshSessionService(RefreshTokenResult result) : IRefreshSessionService
    {
        public Task<RefreshTokenResult> ExecuteAsync(RefreshTokenRequest request, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private sealed class FakeLogoutService(LogoutResult result) : ILogoutService
    {
        public Task<LogoutResult> ExecuteAsync(LogoutRequest request, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private sealed class FakePasswordResetService(PasswordResetResult? resetResult) : IPasswordResetService
    {
        public Task RequestAsync(ForgotPasswordRequest request, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PasswordResetResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
            => Task.FromResult(resetResult ?? PasswordResetResult.Failure("invalid_or_expired_token"));
    }

    private sealed class FakeUserAuthenticationRepository(UserAuthenticationData? data) : IUserAuthenticationRepository
    {
        public Task<UserAuthenticationData?> GetByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(data);

        public Task<UserAuthenticationData?> GetByIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(data?.User.Id == userId ? data : null);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
