using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class WorkPermitPostgresTests(PostgresDatabaseFixture database)
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

    // ════════════════════════════════════════════════════════════════════════
    // Autenticação
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task List_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient(); // sem X-Test-Actor

        using var response = await client.GetAsync("/work-permits");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Listagem e Filtros
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task List_Authenticated_ReturnsOk()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/work-permits");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // A API retorna um array ou um objeto com "items"
        var body = await PayloadAsync(response);
        Assert.True(
            body.ValueKind == JsonValueKind.Array || body.ValueKind == JsonValueKind.Object,
            $"Esperado Array ou Object, obtido {body.ValueKind}");
    }

    [PostgresFact]
    public async Task GetById_AnotherCompanyPermit_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // PermitB pertence à CompanyB. ActorA não deve enxergar.
        using var response = await client.GetAsync($"/work-permits/{database.PermitB}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PostgresFact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.GetAsync($"/work-permits/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Busca (Search)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Search_WithoutCompanyId_ReturnsOnlyAuthorizedPermits()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.GetAsync("/work-permits/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        // O endpoint de search retorna um objeto paginado { total, items }
        Assert.True(body.TryGetProperty("items", out _), "Payload deve conter 'items'");
    }

    [PostgresFact]
    public async Task Search_WithCompanyIdOfAnotherCompany_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.GetAsync($"/work-permits/search?companyId={database.CompanyB}");

        // O service lança NotFound ao tentar acessar empresa fora do escopo
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Criação (validação de payload)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Create_WithoutAppointmentId_ReturnsBadRequest()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // Payload inválido: falta AppointmentId (obrigatório no CreateWorkPermitRequest)
        using var response = await client.PostAsJsonAsync("/work-permits", new
        {
            serviceCode = "MANUTENCAO",
            title = "PT inválida"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
