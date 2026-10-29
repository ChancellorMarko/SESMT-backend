using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DocumentTypePostgresTests(PostgresDatabaseFixture database)
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

    private static object CreatePayload(
        string? code = null,
        string? name = "Tipo fictício",
        string category = "NORMATIVO",
        string secrecyLevel = "INTERNO")
    {
        return new
        {
            Code = code ?? $"DT-{Guid.NewGuid():N}",
            Name = name,
            Description = "Descrição fictícia",
            Category = category,
            SecrecyLevel = secrecyLevel,
            RequiresSignature = false,
            RequiresApproval = true,
            PeriodicityInDays = (int?)null,
            RetentionPeriodInYears = (int?)null,
            MetadataSchema = "{}",
            SchemaVersion = 1
        };
    }

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task AssertNot5xx(HttpResponseMessage response, string endpoint)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            (int)response.StatusCode < 500,
            $"Endpoint {endpoint} retornou {(int)response.StatusCode}: {body}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Autenticação
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task List_AnonymousRequest_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient(); // sem X-Test-Actor

        using var response = await client.GetAsync("/document-types");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CRUD
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Create_WithValidData_Returns201AndLocation()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        var payload = CreatePayload(code: $"DT-{Guid.NewGuid():N}");
        using var response = await client.PostAsJsonAsync("/document-types", payload);

        await AssertNot5xx(response, "POST /document-types");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("Tipo fictício", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("active").GetBoolean());
    }

    [PostgresFact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        var code = $"DT-{Guid.NewGuid():N}";

        // Primeira criação
        using (var first = await client.PostAsJsonAsync("/document-types", CreatePayload(code: code)))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        // Segunda criação com o mesmo código
        using var response = await client.PostAsJsonAsync("/document-types", CreatePayload(code: code));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("DUPLICATE", body.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Create_WithEmptyName_Returns400()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/document-types", CreatePayload(name: ""));

        await AssertNot5xx(response, "POST /document-types (invalid)");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Create_WithMedicalSecrecyAndRequiresApproval_Returns201()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        var payload = CreatePayload(
            code: $"DT-{Guid.NewGuid():N}",
            name: "Tipo Médico",
            category: "SAUDE",
            secrecyLevel: "MEDICO");

        using var response = await client.PostAsJsonAsync("/document-types", payload);

        await AssertNot5xx(response, "POST /document-types (medical)");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [PostgresFact]
    public async Task GetById_ReturnsCreatedType()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        Guid id;
        using (var create = await client.PostAsJsonAsync("/document-types", CreatePayload()))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            id = (await PayloadAsync(create)).GetProperty("id").GetGuid();
        }

        using var response = await client.GetAsync($"/document-types/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync($"/document-types/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Listagem / filtros
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task List_ReturnsSeededTypes()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/document-types");

        await AssertNot5xx(response, "GET /document-types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // O fixture do PostgresDatabaseFixture seeds o tipo "QA-EVIDENCE"
        var body = await PayloadAsync(response);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Contains(body.EnumerateArray(),
            item => item.GetProperty("code").GetString() == "QA-EVIDENCE");
    }

    [PostgresFact]
    public async Task List_FilteredByCategory_ReturnsOnlyMatching()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        // Cria um tipo em uma categoria específica
        var category = $"CUSTOM-{Guid.NewGuid():N}";
        using (var create = await client.PostAsJsonAsync(
            "/document-types", CreatePayload(code: $"DT-{Guid.NewGuid():N}", category: category)))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        using var response = await client.GetAsync($"/document-types?category={category}");

        await AssertNot5xx(response, "GET /document-types?category=...");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.All(body.EnumerateArray(),
            item => Assert.Equal(category, item.GetProperty("category").GetString()));
    }

    [PostgresFact]
    public async Task List_OnlyActiveFiltersOutDeactivated()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        // Cria e desativa um tipo
        Guid id;
        using (var create = await client.PostAsJsonAsync("/document-types", CreatePayload()))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            id = (await PayloadAsync(create)).GetProperty("id").GetGuid();
        }
        using (var deactivate = await client.DeleteAsync($"/document-types/{id}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        }

        // Lista apenas ativos → não deve incluir o desativado
        using var response = await client.GetAsync("/document-types?onlyActive=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.DoesNotContain(body.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Update / Deactivate
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Update_ChangesName()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        Guid id;
        using (var create = await client.PostAsJsonAsync("/document-types", CreatePayload()))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            id = (await PayloadAsync(create)).GetProperty("id").GetGuid();
        }

        var update = new
        {
            Name = "Nome atualizado",
            Description = "Descrição atualizada",
            Category = "NORMATIVO",
            SecrecyLevel = "INTERNO",
            RequiresSignature = true,
            RequiresApproval = true,
            PeriodicityInDays = (int?)30,
            RetentionPeriodInYears = (int?)5,
            MetadataSchema = "{}",
            SchemaVersion = 1
        };

        using var response = await client.PatchAsJsonAsync($"/document-types/{id}", update);

        await AssertNot5xx(response, "PATCH /document-types/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.Equal("Nome atualizado", body.GetProperty("name").GetString());
    }

    [PostgresFact]
    public async Task Deactivate_MarksAsDeleted()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        Guid id;
        using (var create = await client.PostAsJsonAsync("/document-types", CreatePayload()))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            id = (await PayloadAsync(create)).GetProperty("id").GetGuid();
        }

        using var deactivate = await client.DeleteAsync($"/document-types/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        using var getDeleted = await client.GetAsync($"/document-types/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeleted.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Catálogo de serviços (WorkPermits)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task WorkPermitServices_ReturnsCatalog()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        // Rota correta: /work-permits/services (não /work-permits-services)
        using var response = await client.GetAsync("/work-permits/services");

        await AssertNot5xx(response, "GET /work-permits/services");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
