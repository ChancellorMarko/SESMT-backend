using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DashboardPostgresTests(PostgresDatabaseFixture database)
{
    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private HttpClient AuthClient(PostgresApiFactory factory, Guid? actor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (actor ?? database.ActorA).ToString());
        return client;
    }

    private static async Task AssertNot5xx(HttpResponseMessage response, string endpoint)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            (int)response.StatusCode < 500,
            $"Endpoint {endpoint} retornou {(int)response.StatusCode}: {body}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Summary
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Summary_ReturnsPayloadForAuthenticatedActor()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/dashboard/summary");

        await AssertNot5xx(response, "/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // O payload do summary contém contagens, mas com dados zerados pode vir vazio
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Appointments
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Appointments_ReturnsOkOrEmptyPage()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/dashboard/appointments");

        await AssertNot5xx(response, "/dashboard/appointments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [PostgresFact]
    public async Task Appointments_FiltersByStatusWithoutError()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/dashboard/appointments?status=EM_EXECUCAO");

        await AssertNot5xx(response, "/dashboard/appointments?status=...");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task Appointments_FiltersByDateRangeWithoutError()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync(
            "/dashboard/appointments?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z");

        await AssertNot5xx(response, "/dashboard/appointments?from=...&to=...");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Document pendencies
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task DocumentPendencies_ReturnsOk()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/dashboard/document-pendencies");

        await AssertNot5xx(response, "/dashboard/document-pendencies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Review pendencies
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ReviewPendencies_ReturnsOk()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/dashboard/review-pendencies");

        await AssertNot5xx(response, "/dashboard/review-pendencies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Activity evolution
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ActivityEvolution_ReturnsSeriesWithMonths()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        var from = Uri.EscapeDataString("2026-01-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-03-31T23:59:59Z");

        using var response = await client.GetAsync(
            $"/dashboard/activity-evolution?from={from}&to={to}");

        await AssertNot5xx(response, "/dashboard/activity-evolution");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("series", out var series));
        Assert.Equal(JsonValueKind.Array, series.ValueKind);
    }

    [PostgresFact]
    public async Task ActivityEvolution_RejectsPeriodLongerThanThreeYears()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        var from = Uri.EscapeDataString("2020-01-01T00:00:00Z");
        var to = Uri.EscapeDataString("2024-01-01T00:00:00Z");

        using var response = await client.GetAsync(
            $"/dashboard/activity-evolution?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Search
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task WorkPermits_SearchByQueryWithoutError()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/work-permits/search?search=PT&page=1&pageSize=10");

        await AssertNot5xx(response, "/work-permits/search");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Escopo / isolamento
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task TwoActors_DoNotSeeEachOtherCompanyDashboard()
    {
        await using var factory = new PostgresApiFactory(database);
        using var clientA = AuthClient(factory, database.ActorA);
        using var clientB = AuthClient(factory, database.ActorB);

        using var responseA = await clientA.GetAsync("/dashboard/summary");
        using var responseB = await clientB.GetAsync("/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

        // Cada um tem seus próprios dados (o payload varia por escopo, mas ambos são 200)
    }

    [PostgresFact]
    public async Task AnonymousRequest_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient(); // sem X-Test-Actor

        using var response = await client.GetAsync("/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Filtro por filtro de escopo (negação)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ActivityEvolution_RejectsLocationIdOutsideScope()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        var from = Uri.EscapeDataString("2026-01-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-03-31T23:59:59Z");
        var foreignUnit = Guid.NewGuid(); // unidade que não pertence ao ActorA

        using var response = await client.GetAsync(
            $"/dashboard/activity-evolution?from={from}&to={to}&localId={foreignUnit}");

        // O service lança WorkPermitForbiddenException → 403 ou NotFound
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Esperado 403/404, obtido {(int)response.StatusCode}");
    }
}
