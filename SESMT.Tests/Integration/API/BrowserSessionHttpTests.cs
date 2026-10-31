using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SESMT.API.Controllers;
using SESMT.API.Services;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Infrastructure.Identity;

namespace SESMT.Tests.Integration.API;

// Real HTTP routing, JWT validation, antiforgery and cookies; persistence/services use explicit fakes.
// These tests do not validate PostgreSQL or claim a completed business-module integration.
public sealed class BrowserSessionHttpTests
{
    [Fact]
    public async Task LoginRefreshLogout_UseCookieAndNeverExposeRefreshTokenInJson()
    {
        await using var host = await TestHost.StartAsync();
        var csrf = await host.CsrfAsync();
        var login = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, csrf);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", session.GetProperty("tokenType").GetString());
        Assert.False(session.TryGetProperty("refreshToken", out _));
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/browser/auth", cookie, StringComparison.OrdinalIgnoreCase);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());

        var me = await host.Client.GetFromJsonAsync<JsonElement>("/api/v1/browser/me");
        Assert.Equal(host.Services.UserId.ToString(), me.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal("ADMIN", me.GetProperty("roles")[0].GetString());
        Assert.Empty(me.GetProperty("permissions").EnumerateArray());
        Assert.Equal(host.Services.CompanyId.ToString(), me.GetProperty("scope").GetProperty("companyIds")[0].GetString());
        Assert.Empty(me.GetProperty("scope").GetProperty("unitIds").EnumerateArray());

        // CSRF obtained before login remains valid when a bearer identity is now present.
        var refresh = await host.PostAsync("refresh", null, csrf);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal("refresh-original", host.Services.LastRefresh);
        Assert.False((await refresh.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("refreshToken", out _));
        var logout = await host.PostAsync("logout", null, csrf);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("refresh-rotated", host.Services.LastLogout);
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(logout.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task MissingAndForgedCsrf_RejectBeforeAuthenticationOrRotation()
    {
        await using var host = await TestHost.StartAsync();
        var missing = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" });
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal("CSRF_INVALID", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await host.CsrfAsync();
        var forged = await host.PostAsync("refresh", null, "forged-token");
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Equal(0, host.Services.LoginCalls);
        Assert.Equal(0, host.Services.RefreshCalls);
    }

    [Fact]
    public async Task LogoutWithoutCsrf_RejectsBeforeRevocation()
    {
        await using var host = await TestHost.StartAsync();
        await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, await host.CsrfAsync());
        var result = await host.PostAsync("logout", null);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Null(host.Services.LastLogout);
        Assert.False(result.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task LogoutFailure_PreservesCookieForRetryAndDoesNotClaimSessionEnded()
    {
        await using var host = await TestHost.StartAsync();
        var csrf = await host.CsrfAsync();
        await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, csrf);
        host.Services.RejectLogout = true;
        var result = await host.PostAsync("logout", null, csrf);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("LOGOUT_FAILED", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.False(result.Headers.Contains("Set-Cookie"));
        host.Services.RejectLogout = false;
        Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("logout", null, csrf)).StatusCode);
        Assert.Equal("refresh-original", host.Services.LastLogout);
    }

    [Fact]
    public async Task RefreshWithoutCookie_RejectsBodyToken()
    {
        await using var host = await TestHost.StartAsync();
        var csrf = await host.CsrfAsync();
        var result = await host.PostAsync("refresh", new { refreshToken = "client-controlled-body-token" }, csrf);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(0, host.Services.RefreshCalls);
        Assert.Equal("SESSION_EXPIRED", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task MfaRequirement_IsExplicitRejectionWithoutFakeChallenge()
    {
        await using var host = await TestHost.StartAsync();
        host.Services.RequireMfa = true;
        var result = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, await host.CsrfAsync());
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        var error = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MFA_REQUIRED", error.GetProperty("code").GetString());
        Assert.False(error.TryGetProperty("challengeId", out _));
        Assert.False(result.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task WorkerWithoutUniqueLink_RejectsAndRevokesNewSession()
    {
        await using var host = await TestHost.StartAsync();
        host.Services.Roles = ["TRABALHADOR"];
        var result = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, await host.CsrfAsync());
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("WORKER_LINK_REQUIRED", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal("refresh-original", host.Services.LastLogout);
        Assert.DoesNotContain("refresh-original", string.Join(";", result.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task WorkerWithLink_HasOwnScopeAndNoUnadaptedModuleGrants()
    {
        await using var host = await TestHost.StartAsync();
        host.Services.Roles = ["TRABALHADOR"];
        host.Services.WorkerId = Guid.NewGuid();
        var result = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, await host.CsrfAsync());
        var session = await result.Content.ReadFromJsonAsync<JsonElement>();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        var me = await host.Client.GetFromJsonAsync<JsonElement>("/api/v1/browser/me");
        Assert.True(me.GetProperty("scope").GetProperty("ownOnly").GetBoolean());
        Assert.Equal(host.Services.WorkerId.ToString(), me.GetProperty("workerId").GetString());
        Assert.Empty(me.GetProperty("permissions").EnumerateArray());
    }

    [Fact]
    public async Task InvalidCredentials_AreGenericAndNeverSetRefreshCookie()
    {
        await using var host = await TestHost.StartAsync();
        host.Services.InvalidCredentials = true;
        var result = await host.PostAsync("login", new { email = "unknown@example.test", password = "sample-password" }, await host.CsrfAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.False(result.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Production_CookiesRequireHttps()
    {
        await using var host = await TestHost.StartAsync(production: true);
        var result = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, await host.CsrfAsync());
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("secure", Assert.Single(result.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevokedRefresh_ClearsCookieAndDoesNotReturnAccessToken()
    {
        await using var host = await TestHost.StartAsync();
        var csrf = await host.CsrfAsync();
        await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, csrf);
        host.Services.RejectRefresh = true;
        var result = await host.PostAsync("refresh", null, csrf);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SESSION_EXPIRED", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(result.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task MfaEnabledDuringSession_RejectsRefreshAndRevokesRotatedToken()
    {
        await using var host = await TestHost.StartAsync();
        var csrf = await host.CsrfAsync();
        await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, csrf);
        host.Services.MfaEnabled = true;
        var result = await host.PostAsync("refresh", null, csrf);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("MFA_REQUIRED", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal("refresh-rotated", host.Services.LastLogout);
        Assert.DoesNotContain("refresh-rotated", string.Join(";", result.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task AmbiguousWorkerLinks_RejectInsteadOfChoosingFirstWorker()
    {
        await using var host = await TestHost.StartAsync();
        host.Services.Roles = ["TRABALHADOR"];
        host.Services.WorkerId = Guid.NewGuid();
        host.Services.AmbiguousWorker = true;
        var result = await host.PostAsync("login", new { email = "integration@example.test", password = "sample-password" }, await host.CsrfAsync());
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("WORKER_LINK_REQUIRED", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task IdentityEndpoint_RequiresSignedBearerToken()
    {
        await using var host = await TestHost.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/browser/me")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged-access-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/browser/me")).StatusCode);
    }

    private sealed class TestHost(WebApplication app, HttpClient client, FakeServices services) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public FakeServices Services { get; } = services;
        private readonly Dictionary<string, string> cookies = new();

        public static async Task<TestHost> StartAsync(bool production = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = production ? "Production" : "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            var services = new FakeServices();
            builder.Services.AddSingleton<ILoginService>(services);
            builder.Services.AddSingleton<IRefreshSessionService>(services);
            builder.Services.AddSingleton<ILogoutService>(services);
            builder.Services.AddSingleton<IBrowserIdentityReader>(services);
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-Token";
                options.Cookie.Name = "ampara.csrf";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = production ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                options.Cookie.Path = "/api/v1/browser";
            });
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = "integration-tests",
                    ValidateAudience = true, ValidAudience = "integration-browser",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(FakeServices.SigningKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(BrowserSessionController).Assembly);
            var app = builder.Build();
            app.UsePathBase("/api/v1");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            var client = app.GetTestClient();
            client.BaseAddress = new Uri(production ? "https://localhost" : "http://localhost");
            return new TestHost(app, client, services);
        }

        public async Task<string> CsrfAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/browser/auth/csrf");
            AddCookies(request);
            var result = await Client.SendAsync(request);
            SaveCookies(result);
            result.EnsureSuccessStatusCode();
            Assert.Contains("no-store", result.Headers.CacheControl?.ToString());
            return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrfToken").GetString()!;
        }

        public async Task<HttpResponseMessage> PostAsync(string action, object? body, string? csrf = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/browser/auth/{action}");
            if (body is not null) request.Content = JsonContent.Create(body);
            if (csrf is not null) request.Headers.Add("X-CSRF-Token", csrf);
            AddCookies(request);
            var response = await Client.SendAsync(request);
            SaveCookies(response);
            return response;
        }

        private void AddCookies(HttpRequestMessage request)
        {
            if (cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        }
        private void SaveCookies(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
            foreach (var value in values)
            {
                var pair = value.Split(';')[0].Split('=', 2);
                if (pair[1].Length == 0) cookies.Remove(pair[0]); else cookies[pair[0]] = pair[1];
            }
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
    }

    private sealed class FakeServices : ILoginService, IRefreshSessionService, ILogoutService, IBrowserIdentityReader
    {
        public const string SigningKey = "integration-only-signing-key-with-at-least-32-bytes";
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid? WorkerId { get; set; }
        public string[] Roles { get; set; } = ["ADMIN"];
        public bool RequireMfa { get; set; }
        public bool InvalidCredentials { get; set; }
        public bool RejectRefresh { get; set; }
        public bool RejectLogout { get; set; }
        public bool MfaEnabled { get; set; }
        public bool AmbiguousWorker { get; set; }
        public int LoginCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public string? LastRefresh { get; private set; }
        public string? LastLogout { get; private set; }

        public Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
        {
            LoginCalls++;
            return Task.FromResult(RequireMfa ? LoginResult.Failure("mfa_required")
                : InvalidCredentials ? LoginResult.Failure("invalid_credentials") : LoginResult.Success(Response("refresh-original")));
        }
        public Task<RefreshTokenResult> ExecuteAsync(RefreshTokenRequest request, CancellationToken ct = default)
        {
            RefreshCalls++; LastRefresh = request.RefreshToken;
            return Task.FromResult(RejectRefresh ? RefreshTokenResult.Failure("session_revoked") : RefreshTokenResult.Success(Response("refresh-rotated")));
        }
        public Task<LogoutResult> ExecuteAsync(LogoutRequest request, CancellationToken ct = default)
        { LastLogout = request.RefreshToken; return Task.FromResult(RejectLogout ? LogoutResult.Failure("database_unavailable") : LogoutResult.Success()); }
        public Task<BrowserIdentity?> FindAsync(Guid id, CancellationToken ct)
        {
            var user = new User("Integration User", "integration@example.test", "test-only-hash");
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, UserId);
            if (MfaEnabled) user.EnableMfa([1]);
            return Task.FromResult<BrowserIdentity?>(new BrowserIdentity(new UserAuthenticationData(user, Roles, [new("EMPRESA", CompanyId)]), WorkerId, AmbiguousWorker));
        }
        private LoginResponse Response(string refresh)
        {
            var tokens = new JwtTokenService(Options.Create(new JwtSettings
            {
                SigningKey = SigningKey, Issuer = "integration-tests", Audience = "integration-browser", AccessTokenExpirationMinutes = 5
            }));
            var scopes = new[] { new UserScopeClaim("EMPRESA", CompanyId) };
            var token = tokens.GenerateAccessToken(UserId, Roles, scopes);
            return new LoginResponse(token.Token, token.ExpiresAtUtc, refresh, DateTimeOffset.UtcNow.AddDays(1), UserId, Roles, scopes);
        }
    }
}
