using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class ErrorContractPostgresTests(PostgresDatabaseFixture database)
{
    private async Task<HttpClient> Admin(PostgresApiFactory factory)
    {
        await using var db = database.CreateContext(); var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (await db.Users.SingleAsync(u => u.Email.Value == "seed-admin@example.invalid")).Id.ToString());
        return client;
    }

    [PostgresFact]
    public async Task InvalidNativeBodiesUseTheStandardErrorShapeWithFieldsAndNoFrameworkText()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory);
        using var malformed = await client.PostAsync("/api/v1/trainings", new StringContent("{\"unitId\": \"não-é-guid\", \"durationMinutes\": \"x\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        var text = await malformed.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal("VALIDATION_FAILED", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Confira os dados informados.", body.RootElement.GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("requestId").GetString()));
        var fields = body.RootElement.GetProperty("fieldErrors").EnumerateArray().ToArray();
        Assert.NotEmpty(fields);
        Assert.All(fields, field => Assert.Equal("Valor inválido ou ausente.", field.GetProperty("message").GetString()));
        Assert.DoesNotContain("JSON", text); Assert.DoesNotContain("não-é-guid", text); Assert.DoesNotContain("\"title\"", text);
    }

    [PostgresFact]
    public async Task AnInvalidTrustedProxyAddressStopsTheStartup()
    {
        await using var factory = new PostgresApiFactory(database);
        await using var misconfigured = factory.WithWebHostBuilder(builder => builder.UseSetting("ForwardedHeaders:KnownProxies:0", "não-é-ip"));
        var error = Assert.ThrowsAny<Exception>(() => misconfigured.CreateClient());
        Assert.Contains("KnownProxies", error.ToString());
    }
}
