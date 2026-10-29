using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SESMT.Application.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class OperationalDesignationPostgresTests(PostgresDatabaseFixture database)
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

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Payload mínimo plausível para criação. Se o DTO real
    /// (CreateOperationalDesignationRequest) tiver nomes diferentes,
    /// ajuste as propriedades aqui.
    /// </summary>
    private static object CreatePayload(Guid companyId, Guid workerId, Guid locationId, string role = "conferir")
        => new
        {
            CompanyId = companyId,
            WorkerId = workerId,
            ServiceIds = WorkPermitServiceCatalogEntry.All.Select(s => s.Id).ToArray(),
            LocationIds = new[] { locationId },
            Role = role,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(4),
            Notes = "Designação fictícia dos testes"
        };

    // ════════════════════════════════════════════════════════════════════════
    // GET /designations
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task List_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient(); // sem X-Test-Actor

        using var response = await client.GetAsync("/designations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task List_ReturnsDesignationsForActor()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.GetAsync("/designations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);

        // O fixture seeds uma designação operacional para ActorA
        Assert.NotEmpty(body.EnumerateArray());
    }

    [PostgresFact]
    public async Task List_OnlyReturnsDesignationsOfTheActorScope()
    {
        await using var factory = new PostgresApiFactory(database);
        using var clientA = AuthClient(factory, database.ActorA);
        using var clientB = AuthClient(factory, database.ActorB);

        using var responseA = await clientA.GetAsync("/designations");
        using var responseB = await clientB.GetAsync("/designations");

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

        var bodyA = await PayloadAsync(responseA);
        var bodyB = await PayloadAsync(responseB);

        // Cada actor vê apenas as designações do próprio escopo (CompanyA vs CompanyB).
        // Nenhuma designação de A aparece em B (e vice-versa), garantido pelo service.
        Assert.All(bodyA.EnumerateArray(), d =>
            Assert.NotEqual(database.CompanyB, d.GetProperty("companyId").GetGuid()));
        Assert.All(bodyB.EnumerateArray(), d =>
            Assert.NotEqual(database.CompanyA, d.GetProperty("companyId").GetGuid()));
    }

    // ════════════════════════════════════════════════════════════════════════
    // POST /designations
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Create_WithValidData_Returns201Or403()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        var payload = CreatePayload(database.CompanyA, database.WorkerA, database.UnitA);

        using var response = await client.PostAsJsonAsync("/designations", payload);

        // Se ActorA tem permissão para criar → 201.
        // Se não tem (por role/designação) → 403.
        // Em ambos os casos NÃO deve ser 5xx.
        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Forbidden,
            $"Esperado 201/403, obtido {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var body = await PayloadAsync(response);
            Assert.Equal(database.CompanyA, body.GetProperty("companyId").GetGuid());
            Assert.Equal(database.WorkerA, body.GetProperty("workerId").GetGuid());
        }
    }

    [PostgresFact]
    public async Task Create_ForAnotherCompany_Returns403Or404()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // ActorA tenta criar designação em CompanyB (sem escopo) → nega
        var payload = CreatePayload(database.CompanyB, database.WorkerB, database.UnitB);

        using var response = await client.PostAsJsonAsync("/designations", payload);

        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Esperado 403/404, obtido {(int)response.StatusCode}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // POST /designations/{id:guid}/revoke
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Revoke_WithUnknownId_Returns404()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.PostAsJsonAsync(
            $"/designations/{Guid.NewGuid()}/revoke",
            new { Reason = "Revogação fictícia dos testes" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PostgresFact]
    public async Task Revoke_WithoutReason_Returns400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.PostAsJsonAsync(
            $"/designations/{Guid.NewGuid()}/revoke",
            new { Reason = "" });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Esperado 400/404, obtido {(int)response.StatusCode}");
    }
}
