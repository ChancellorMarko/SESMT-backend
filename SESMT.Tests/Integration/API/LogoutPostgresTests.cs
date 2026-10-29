using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class LogoutPostgresTests(PostgresDatabaseFixture database)
{
    // ⚠️ Credenciais exatas do seeder configurado em PostgresApiFactory
    private const string SeededAdminEmail = "seed-admin@example.invalid";
    private const string SeededAdminPassword = "TestOnly-NotAnOperationalCredential-123!";

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Client sem CookieContainer automático — enviamos cookies manualmente
    /// para não depender do flag Secure (que bloqueia envio via HTTP).
    /// </summary>
    private static HttpClient CreateClient(PostgresApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

    /// <summary>
    /// Extrai o valor de um cookie do header Set-Cookie da resposta.
    /// </summary>
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

    /// <summary>
    /// Login real, extrai accessToken e cookies.
    /// </summary>
    private static async Task<(string AccessToken, string RefreshCookie, string CsrfCookie)> LoginAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = SeededAdminEmail,
            password = SeededAdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        var accessToken = body.GetProperty("accessToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        var refreshCookie = ExtractCookie(response, "sesmt_refresh");
        var csrfCookie = ExtractCookie(response, "sesmt_csrf");

        Assert.NotNull(refreshCookie);
        Assert.NotNull(csrfCookie);

        return (accessToken, refreshCookie!, csrfCookie!);
    }

    /// <summary>
    /// Constrói um POST /auth/logout com os cookies da sessão.
    /// </summary>
    private static HttpRequestMessage BuildLogout(string refreshCookie, string csrfCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Cookie", $"sesmt_refresh={refreshCookie}; sesmt_csrf={csrfCookie}");
        return request;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Logout sem cookies
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
    // Logout com sessão válida — fluxo completo
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Logout_WithValidCookies_RevokesSessionAndClearsCookies()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        using var response = await client.SendAsync(BuildLogout(refreshCookie, csrfCookie));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookies = string.Join(
            "; ",
            response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>());
        Assert.Contains("sesmt_refresh=", setCookies);
        Assert.Contains("sesmt_csrf=", setCookies);
        Assert.True(
            setCookies.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase)
            || setCookies.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase),
            $"Esperado header de deleção. Recebido: {setCookies}");

        // ⚠️ Sessão revogada no banco
        await using var db = database.CreateContext();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email.Value == SeededAdminEmail);
        Assert.NotNull(user);

        var session = await db.UserSessions
            .Where(s => s.UserId == user!.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(session);
        Assert.NotNull(session!.RevokedAt);
        Assert.False(session.Active);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Login após logout
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Logout_ThenLoginAgain_ReturnsNewTokens()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        using (var logout = await client.SendAsync(BuildLogout(refreshCookie, csrfCookie)))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        // Novo login — deve funcionar mesmo após revogação da sessão anterior
        using var newLogin = await client.PostAsJsonAsync("/auth/login", new
        {
            email = SeededAdminEmail,
            password = SeededAdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var body = await PayloadAsync(newLogin);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        // Cookies devem ser regenerados
        Assert.NotNull(ExtractCookie(newLogin, "sesmt_refresh"));
        Assert.NotNull(ExtractCookie(newLogin, "sesmt_csrf"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Idempotência (chamado duas vezes)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Logout_CalledTwiceWithSameCookies_Returns204BothTimes()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        using (var first = await client.SendAsync(BuildLogout(refreshCookie, csrfCookie)))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        // Segunda chamada: refreshToken já revogado → LogoutService retorna Success (no-op)
        using (var second = await client.SendAsync(BuildLogout(refreshCookie, csrfCookie)))
        {
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        }
    }

    [PostgresFact]
    public async Task Logout_ConcurrentRequestsWithSameCookies_BothReturn204()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        var (_, refreshCookie, csrfCookie) = await LoginAsync(client);

        var responses = await Task.WhenAll(
            client.SendAsync(BuildLogout(refreshCookie, csrfCookie)),
            client.SendAsync(BuildLogout(refreshCookie, csrfCookie)));

        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Token inválido (cookie presente mas token não revogável)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Logout_WithInvalidRefreshCookie_Returns204NoOp()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = CreateClient(factory);

        using var response = await client.SendAsync(
            BuildLogout(refreshCookie: "invalid-token", csrfCookie: "any-csrf"));

        // Token inválido → sessão não encontrada → LogoutService retorna Success
        // O controller limpa os cookies mesmo assim
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
