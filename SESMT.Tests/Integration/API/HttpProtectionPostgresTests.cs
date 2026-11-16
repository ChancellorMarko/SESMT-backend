using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class HttpProtectionPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task LoginIsLimitedPerClientAndAnswersWithTheStandardErrorContract()
    {
        await using var factory = new PostgresApiFactory(database);
        await using var limited = factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimiting:EnabledInTesting", "true").UseSetting("RateLimiting:AuthenticationPerMinute", "2"));
        using var client = limited.CreateClient();
        var body = new { email = "ninguem@example.invalid", password = "senha-incorreta" };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var allowed = await client.PostAsJsonAsync("/api/v1/auth/login", body);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }
        using var blocked = await client.PostAsJsonAsync("/api/v1/auth/login", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("60", blocked.Headers.RetryAfter?.ToString());
        using var error = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        Assert.Equal("TOO_MANY_REQUESTS", error.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.RootElement.GetProperty("requestId").GetString()));

        // Other endpoints are not affected by the authentication bucket.
        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // Session renewal has its own bucket: an exhausted login limit must not sign people out.
        using var refresh = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, refresh.StatusCode);
        using var browserRefresh = await client.PostAsync("/api/v1/browser/auth/refresh", null);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, browserRefresh.StatusCode);
    }

    [PostgresFact]
    public async Task ResponsesCarryProtectionHeadersAndBlockedAccountsLookLikeWrongPasswords()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ninguem@example.invalid", password = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_CREDENTIALS", error.RootElement.GetProperty("code").GetString());
    }
}
