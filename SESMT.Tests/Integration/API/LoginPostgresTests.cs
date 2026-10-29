using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class LoginPostgresTests(PostgresDatabaseFixture database)
{
    // ⚠️ Credenciais exatas do seeder configurado em PostgresApiFactory
    private const string SeededAdminEmail = "seed-admin@example.invalid";
    private const string SeededAdminPassword = "TestOnly-NotAnOperationalCredential-123!";

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cria um client com CookieContainer habilitado, permitindo round-trip de
    /// Set-Cookie → Cookie automaticamente entre requests.
    /// </summary>
    private static HttpClient CreateClient(PostgresApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Login
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Login_WithSeededAdmin_ReturnsJwtAndSetsCookies()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = SeededAdminEmail,
            password = SeededAdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.True(body.TryGetProperty("accessToken", out var accessToken));
        Assert.False(string.IsNullOrWhiteSpace(accessToken.GetString()));
        Assert.True(body.TryGetProperty("userId", out _));
        Assert.True(body.TryGetProperty("roles", out _));
        Assert.True(body.TryGetProperty("scopes", out _));

        // ⚠️ refreshToken NÃO pode estar no JSON — é cookie HttpOnly
        Assert.False(body.TryGetProperty("refreshToken", out _));

        // Set-Cookie: refresh (HttpOnly) + csrf (JS-readable)
        var setCookies = string.Join(
            "; ",
            response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>());
        Assert.Contains("sesmt_refresh=", setCookies);
        Assert.Contains("sesmt_csrf=", setCookies);
        Assert.Contains("HttpOnly", setCookies);
    }

    [PostgresFact]
    public async Task Login_WithUnknownEmail_Returns401InvalidCredentials()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = "unknown@example.invalid",
            password = SeededAdminPassword
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Login_WithWrongPassword_Returns401InvalidCredentials()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = SeededAdminEmail,
            password = "WrongPassword-NotTheRealOne!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Login_WithEmptyBody_Returns400Or401()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/auth/login", new { });

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
            $"Esperado 400/401, obtido {(int)response.StatusCode}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // CSRF
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Csrf_ReturnsTokenAndSetsCookie()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("csrfToken").GetString()));

        var setCookies = string.Join(
            "; ",
            response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>());
        Assert.Contains("sesmt_csrf=", setCookies);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Refresh
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Refresh_WithoutCookies_Returns401NoRefreshToken()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsync("/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("NO_REFRESH_TOKEN", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Refresh_WithCsrfCookieButNoRefreshCookie_Returns401()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        // Popula cookie sesmt_csrf (CookieContainer guarda automaticamente)
        using (var csrfResponse = await client.GetAsync("/auth/csrf"))
        {
            Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
            var csrfBody = await PayloadAsync(csrfResponse);
            var csrfToken = csrfBody.GetProperty("csrfToken").GetString();

            // Envia como header — o refresh valida csrf antes de ler refresh cookie
            client.DefaultRequestHeaders.Add("X-CSRF-Token", csrfToken);
        }

        using var response = await client.PostAsync("/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("NO_REFRESH_TOKEN", body.GetProperty("code").GetString());
    }

    // ════════════════════════════════════════════════════════════════════════
    // Logout
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Logout_WithoutCookies_Returns204NoOp()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Forgot password
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ForgotPassword_Returns202ForKnownAndUnknownEmail()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        // Ambos devem retornar 202 (anti-enumeration)
        using var knownResponse = await client.PostAsJsonAsync(
            "/auth/forgot-password", new { email = SeededAdminEmail });
        using var unknownResponse = await client.PostAsJsonAsync(
            "/auth/forgot-password", new { email = "does-not-exist@example.invalid" });

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        // Mensagem genérica idêntica nos dois casos
        var knownBody = await PayloadAsync(knownResponse);
        var unknownBody = await PayloadAsync(unknownResponse);
        Assert.Equal(
            knownBody.GetProperty("message").GetString(),
            unknownBody.GetProperty("message").GetString());
    }

    // ════════════════════════════════════════════════════════════════════════
    // Reset password (payload validation)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = "invalid-token",
            newPassword = "NovaSenhaSegura-2026!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("INVALID_RESET_TOKEN", body.GetProperty("code").GetString());
    }
}
