using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class RefreshSessionPostgresTests(PostgresDatabaseFixture database)
{
    private const string SeededAdminEmail = "seed-admin@example.invalid";
    private const string SeededAdminPassword = "TestOnly-NotAnOperationalCredential-123!";

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private static HttpClient CreateClient(PostgresApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

    private static string? ExtractCookie(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        var prefix = $"{cookieName}=";
        foreach (var header in values)
        {
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var start = prefix.Length;
            var end = header.IndexOf(';', start);
            return (end > 0 ? header[start..end] : header[start..]).Trim();
        }
        return null;
    }

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<(string AccessToken, string RefreshCookie, string CsrfCookie)> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = SeededAdminEmail,
            password = SeededAdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var refresh = ExtractCookie(response, "sesmt_refresh");
        var csrf = ExtractCookie(response, "sesmt_csrf");

        Assert.NotNull(refresh);
        Assert.NotNull(csrf);

        return (accessToken, refresh!, csrf!);
    }

    private static HttpRequestMessage BuildRefresh(string refreshCookie, string csrfCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("Cookie", $"sesmt_refresh={refreshCookie}; sesmt_csrf={csrfCookie}");
        request.Headers.Add("X-CSRF-Token", csrfCookie);
        return request;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Testes
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Refresh_WithoutCookies_Returns401()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.PostAsync("/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("NO_REFRESH_TOKEN", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Refresh_WithoutCsrfHeader_Returns403()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        // Envia refresh cookie + csrf cookie, mas SEM header X-CSRF-Token
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("Cookie", $"sesmt_refresh={refreshCookie}; sesmt_csrf={csrfCookie}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [PostgresFact]
    public async Task Refresh_WithValidSession_ReturnsNewTokensAndRotatesCookie()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        using var response = await client.SendAsync(BuildRefresh(refreshCookie, csrfCookie));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        // Cookie novo (diferente do anterior)
        var newRefresh = ExtractCookie(response, "sesmt_refresh");
        Assert.NotNull(newRefresh);
        Assert.NotEqual(refreshCookie, newRefresh);

        // Sessão antiga deve estar revogada no banco
        await using var db = database.CreateContext();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email.Value == SeededAdminEmail);
        Assert.NotNull(user);

        var sessions = await db.UserSessions
            .Where(s => s.UserId == user!.Id)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        // A primeira (login) → revogada. A segunda (refresh) → ativa.
        Assert.Contains(sessions, s => s.RevokedAt is not null);
        Assert.Contains(sessions, s => s.RevokedAt is null);
    }

    [PostgresFact]
    public async Task Refresh_AfterLogout_Returns401()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        // Logout (revoga sessão + limpa cookies)
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"sesmt_refresh={refreshCookie}; sesmt_csrf={csrfCookie}");

        using (var logout = await client.SendAsync(logoutRequest))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        // Refresh com o mesmo cookie → deve falhar
        using var response = await client.SendAsync(BuildRefresh(refreshCookie, csrfCookie));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("SESSION_EXPIRED", body.GetProperty("code").GetString());
    }
}
