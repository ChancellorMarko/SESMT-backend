using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;

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

    /// <summary>Creates an account whose role grants <c>parametro:editar</c>; global when
    /// <paramref name="companyId"/> is null, otherwise restricted to that company.</summary>
    private async Task<Guid> CatalogEditorAsync(Guid? companyId = null)
    {
        await using var db = database.CreateContext();
        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Resource == "parametro" && p.Action == "editar");
        if (permission is null)
        {
            permission = new Permission("parametro", "editar", "Permissão de teste isolado");
            db.Add(permission);
        }
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User($"Configurador fictício {suffix}", $"catalog-{suffix}@example.invalid", "not-a-login-hash");
        var role = new Role($"QA_CATALOG_{suffix}", "Catálogo de teste", "Somente para os testes isolados");
        db.AddRange(user, role);
        db.Add(new RolePermission(role.Id, permission.Id));
        db.Add(new UserRole(user.Id, role.Id, companyId));
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<HttpClient> ManagerClientAsync(PostgresApiFactory factory)
        => AuthClient(factory, await CatalogEditorAsync());

    private async Task<(Guid Id, JsonElement Body)> CreateTypeAsync(HttpClient client, object? payload = null)
    {
        using var create = await client.PostAsJsonAsync("/document-types", payload ?? CreatePayload());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var body = await PayloadAsync(create);
        return (body.GetProperty("id").GetGuid(), body);
    }

    private static object UpdatePayload(int expectedVersion, string name = "Nome atualizado",
        bool requiresSignature = false, string secrecyLevel = "INTERNO") => new
    {
        ExpectedVersion = expectedVersion,
        Name = name,
        Description = "Descrição atualizada",
        Category = "NORMATIVO",
        SecrecyLevel = secrecyLevel,
        RequiresSignature = requiresSignature,
        RequiresApproval = true,
        PeriodicityInDays = (int?)null,
        RetentionPeriodInYears = (int?)null,
        MetadataSchema = "{ }",
        SchemaVersion = 1
    };

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
        using var client = await ManagerClientAsync(factory);

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
        using var client = await ManagerClientAsync(factory);

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
        using var client = await ManagerClientAsync(factory);

        using var response = await client.PostAsJsonAsync(
            "/document-types", CreatePayload(name: ""));

        await AssertNot5xx(response, "POST /document-types (invalid)");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Create_WithMedicalSecrecyAndRequiresApproval_Returns201()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);

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
        using var client = await ManagerClientAsync(factory);

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
        using var client = await ManagerClientAsync(factory);

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
        using var client = await ManagerClientAsync(factory);

        using var response = await client.GetAsync("/document-types");

        await AssertNot5xx(response, "GET /document-types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // O fixture do PostgresDatabaseFixture seeds o tipo "QA-EVIDENCE"
        var body = await PayloadAsync(response);
        Assert.Contains(body.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("item").GetProperty("code").GetString() == "QA-EVIDENCE");
    }

    [PostgresFact]
    public async Task List_FilteredByCategory_ReturnsOnlyMatching()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);

        // Cria um tipo em uma categoria específica
        var category = $"CUSTOM-{Guid.NewGuid():N}"[..30];
        using (var create = await client.PostAsJsonAsync(
            "/document-types", CreatePayload(code: $"DT-{Guid.NewGuid():N}", category: category)))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        using var response = await client.GetAsync($"/document-types?q={category}");

        await AssertNot5xx(response, "GET /document-types?q=...");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await PayloadAsync(response)).GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, entry => Assert.Equal(category, entry.GetProperty("item").GetProperty("category").GetString()));
    }

    [PostgresFact]
    public async Task List_OnlyActiveFiltersOutDeactivated()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);

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

        // A listagem paginada traz somente tipos ativos
        using var response = await client.GetAsync("/document-types?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await PayloadAsync(response);
        Assert.DoesNotContain(body.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("id").GetGuid() == id);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Update / Deactivate
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Update_ChangesIdentificationAndIncrementsVersion()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);
        var (id, created) = await CreateTypeAsync(client);
        Assert.Equal(1, created.GetProperty("version").GetInt32());

        using var response = await client.PatchAsJsonAsync($"/document-types/{id}", UpdatePayload(1));

        await AssertNot5xx(response, "PATCH /document-types/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await PayloadAsync(response);
        Assert.Equal("Nome atualizado", body.GetProperty("name").GetString());
        Assert.Equal(2, body.GetProperty("version").GetInt32());

        using var detail = await client.GetAsync($"/document-types/{id}");
        var envelope = await PayloadAsync(detail);
        Assert.Equal(2, envelope.GetProperty("version").GetInt32());
        Assert.Equal(2, envelope.GetProperty("item").GetProperty("version").GetInt32());
    }

    [PostgresFact]
    public async Task Update_WithStaleVersion_Returns409VersionConflict()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);
        var (id, _) = await CreateTypeAsync(client);
        using (var first = await client.PatchAsJsonAsync($"/document-types/{id}", UpdatePayload(1)))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await client.PatchAsJsonAsync($"/document-types/{id}", UpdatePayload(1, "Outro nome"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("VERSION_CONFLICT", (await PayloadAsync(stale)).GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Update_ChangingPolicy_Returns409AndKeepsType()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);
        var (id, _) = await CreateTypeAsync(client);

        foreach (var attempt in new[] { UpdatePayload(1, requiresSignature: true), UpdatePayload(1, secrecyLevel: "RESTRITO") })
        {
            using var response = await client.PatchAsJsonAsync($"/document-types/{id}", attempt);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("IMMUTABLE_DOCUMENT_POLICY", (await PayloadAsync(response)).GetProperty("code").GetString());
        }

        using var detail = await client.GetAsync($"/document-types/{id}");
        var item = (await PayloadAsync(detail)).GetProperty("item");
        Assert.False(item.GetProperty("requiresSignature").GetBoolean());
        Assert.Equal("INTERNO", item.GetProperty("secrecyLevel").GetString());
        Assert.Equal(1, item.GetProperty("version").GetInt32());
    }

    [PostgresFact]
    public async Task History_ListsCreationAndUpdateWithVersions()
    {
        await using var factory = new PostgresApiFactory(database);
        var managerId = await CatalogEditorAsync();
        using var client = AuthClient(factory, managerId);
        var (id, _) = await CreateTypeAsync(client);
        using (var update = await client.PatchAsJsonAsync($"/document-types/{id}", UpdatePayload(1)))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var response = await client.GetAsync($"/document-types/{id}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = (await PayloadAsync(response)).EnumerateArray()
            .Select(e => (Action: e.GetProperty("action").GetString(), Version: e.GetProperty("version").GetInt32(),
                Id: e.GetProperty("id").GetGuid()))
            .ToArray();
        Assert.Contains(("DOCUMENT_TYPE_CREATED", 1), entries.Select(e => (e.Action, e.Version)));
        Assert.Contains(("DOCUMENT_TYPE_UPDATED", 2), entries.Select(e => (e.Action, e.Version)));
        Assert.Equal(entries.Length, entries.Select(e => e.Id).Distinct().Count());
        await using var db = database.CreateContext();
        var actors = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ResourceType == "tipo_documento" && a.ResourceId == id).Select(a => a.ActorId).ToListAsync();
        Assert.All(actors, actor => Assert.Equal(managerId, actor));
    }

    [PostgresFact]
    public async Task Deactivate_RecordsHistoryEvent()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);
        var (id, _) = await CreateTypeAsync(client);
        using (var deactivate = await client.DeleteAsync($"/document-types/{id}"))
            Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        await using var db = database.CreateContext();
        var events = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ResourceType == "tipo_documento" && a.ResourceId == id)
            .Select(a => new { a.Action, a.ResourceVersion }).ToListAsync();
        Assert.Contains(events, e => e.Action == "DOCUMENT_TYPE_DEACTIVATED" && e.ResourceVersion == 2);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Autorização: o catálogo é global
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task ScopedAccounts_CannotChangeCatalogAndSeeReadOnlyContext()
    {
        await using var factory = new PostgresApiFactory(database);
        using var manager = await ManagerClientAsync(factory);
        var (id, _) = await CreateTypeAsync(manager);
        // ActorA holds documento:editar for company A; the second account holds parametro:editar for company A only.
        var scopedActors = new[] { database.ActorA, await CatalogEditorAsync(database.CompanyA) };

        foreach (var actor in scopedActors)
        {
            using var client = AuthClient(factory, actor);
            using var context = await client.GetAsync("/document-types/management-context");
            Assert.Equal(HttpStatusCode.OK, context.StatusCode);
            Assert.False((await PayloadAsync(context)).GetProperty("canManage").GetBoolean());

            using var create = await client.PostAsJsonAsync("/document-types", CreatePayload());
            using var update = await client.PatchAsJsonAsync($"/document-types/{id}", UpdatePayload(1));
            using var delete = await client.DeleteAsync($"/document-types/{id}");
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

            using var read = await client.GetAsync($"/document-types/{id}");
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        using var managerContext = await manager.GetAsync("/document-types/management-context");
        Assert.True((await PayloadAsync(managerContext)).GetProperty("canManage").GetBoolean());
        using var unchanged = await manager.GetAsync($"/document-types/{id}");
        Assert.Equal(1, (await PayloadAsync(unchanged)).GetProperty("version").GetInt32());
    }

    [PostgresFact]
    public async Task Deactivate_MarksAsDeleted()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = await ManagerClientAsync(factory);

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
        using var client = await ManagerClientAsync(factory);

        // Rota correta: /work-permits/services (não /work-permits-services)
        using var response = await client.GetAsync("/work-permits/services");

        await AssertNot5xx(response, "GET /work-permits/services");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
