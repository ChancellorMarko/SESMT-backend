using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class TeamManagementPostgresTests(PostgresDatabaseFixture database)
{
    private async Task<HttpClient> ClientAsync(PostgresApiFactory factory, bool write = true)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        await using var db = database.CreateContext();
        var role = new Role("QA_T_" + Guid.NewGuid().ToString("N"), "Equipe de QA", "Somente teste isolado");
        db.AddRange(role, new UserRole(database.ActorA, role.Id, database.CompanyA));
        foreach (var action in write ? new[] { "ler", "criar", "editar" } : new[] { "ler" })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == "equipe" && p.Action == action && p.Active && p.DeletedAt == null);
            if (permission is null) { permission = new("equipe", action, "Teste isolado"); db.Add(permission); }
            db.Add(new RolePermission(role.Id, permission.Id));
        }
        await db.SaveChangesAsync(); return client;
    }
    private object Payload(string name, Guid? organization = null, Guid[]? participants = null) => new { organizationId = organization,
        companyId = database.CompanyA, localId = database.UnitA, name, description = "Descrição de QA", chiefId = database.WorkerA, participantIds = participants ?? [] };

    private async Task<Guid> CreateAsync(HttpClient client)
    {
        var name = "QA-" + Guid.NewGuid().ToString("N");
        using var response = await client.PostAsJsonAsync("/api/v1/teams", Payload(name, database.CompanyA));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, dto.GetProperty("version").GetInt32());
        return dto.GetProperty("id").GetGuid();
    }

    [PostgresFact]
    public async Task ContextAndCreationUseTheAuthorizedUnitAndCompanyRoot()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory);
        using var context = await client.GetAsync($"/api/v1/teams/management-context?locationId={database.UnitA}&action=criar");
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        var dto = await context.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(database.CompanyA, dto.GetProperty("companyId").GetGuid()); Assert.Equal(database.UnitA, dto.GetProperty("localId").GetGuid());
        Assert.Equal(database.CompanyA, dto.GetProperty("organizationId").GetGuid());
        var id = await CreateAsync(client);
        await using var db = database.CreateContext(); var stored = await db.Teams.SingleAsync(t => t.Id == id);
        Assert.Equal(database.CompanyA, stored.OrganizationId); Assert.Equal(database.CompanyA, stored.CompanyId); Assert.Equal(database.WorkerA, stored.ChiefId);
    }

    [PostgresFact]
    public async Task ContextRejectsOtherCompanyAndUnsupportedOperations()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory);
        using var other = await client.GetAsync($"/api/v1/teams/management-context?locationId={database.UnitB}&action=criar");
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        using var invalid = await client.GetAsync($"/api/v1/teams/management-context?locationId={database.UnitA}&action=excluir");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var anonymous = factory.CreateClient(); using var denied = await anonymous.GetAsync($"/api/v1/teams/management-context?locationId={database.UnitA}&action=criar");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [PostgresFact]
    public async Task ArbitraryOrganizationAndForeignParticipantsAreNotPersisted()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory);
        var name = "QA-" + Guid.NewGuid().ToString("N");
        using var wrong = await client.PostAsJsonAsync("/api/v1/teams", Payload(name, Guid.NewGuid())); Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        using var foreign = await client.PostAsJsonAsync("/api/v1/teams", Payload(name, database.CompanyA, [database.WorkerB])); Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        await using var db = database.CreateContext(); Assert.False(await db.Teams.AnyAsync(t => t.Name == name));
    }

    [PostgresFact]
    public async Task EditChangesChiefRosterAndDescriptionAndProtectsAgainstStaleRevision()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory);
        var id = await CreateAsync(client); Guid workerId;
        await using (var db = database.CreateContext())
        {
            var hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var worker = new Worker(database.CompanyA, "QA-" + hash[..12], [1, 2, 3], hash, "Participante de QA", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, null);
            db.Add(worker); await db.SaveChangesAsync(); workerId = worker.Id;
        }
        using var saved = await client.PatchAsJsonAsync($"/api/v1/teams/{id}", new { expectedVersion = 1, chiefId = workerId, participantIds = new[] { database.WorkerA }, clearDescription = true });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var receipt = await saved.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(2, receipt.GetProperty("version").GetInt32()); Assert.Equal(JsonValueKind.Null, receipt.GetProperty("description").ValueKind);
        using var stale = await client.PatchAsJsonAsync($"/api/v1/teams/{id}", new { expectedVersion = 1, name = "Não sobrescrever" }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var missing = await client.PatchAsJsonAsync($"/api/v1/teams/{id}", new { name = "Sem versão" }); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await using var verify = database.CreateContext(); var team = await verify.Teams.SingleAsync(t => t.Id == id);
        Assert.Equal(workerId, team.ChiefId); Assert.Equal(new[] { database.WorkerA }, team.ParticipantIds); Assert.Equal(2, team.Version); Assert.Null(team.Description);
    }

    [PostgresFact]
    public async Task TwoHttpEditorsCannotOverwriteEachOther()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory); var id = await CreateAsync(client);
        var responses = await Task.WhenAll(client.PatchAsJsonAsync($"/api/v1/teams/{id}", new { expectedVersion = 1, name = "QA-A-" + Guid.NewGuid().ToString("N") }),
            client.PatchAsJsonAsync($"/api/v1/teams/{id}", new { expectedVersion = 1, name = "QA-B-" + Guid.NewGuid().ToString("N") }));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var db = database.CreateContext(); Assert.Equal(2, (await db.Teams.SingleAsync(t => t.Id == id)).Version);
    }

    [PostgresFact]
    public async Task DatabaseRejectsTwoAlreadyLoadedEditors()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory); var id = await CreateAsync(client);
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var a = await first.Teams.SingleAsync(t => t.Id == id); var b = await second.Teams.SingleAsync(t => t.Id == id);
        a.Update(a.Name, a.LocalId, "Vencedor", a.ChiefId, []); b.Update(b.Name, b.LocalId, "Perdedor", b.ChiefId, []);
        await first.SaveChangesAsync(); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verify = database.CreateContext(); Assert.Equal("Vencedor", (await verify.Teams.SingleAsync(t => t.Id == id)).Description);
    }

    [PostgresFact]
    public async Task ScopedPermissionDoesNotEditAnotherCompanysTeam()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory);
        using var response = await client.PatchAsJsonAsync($"/api/v1/teams/{database.TeamB}", new { expectedVersion = 1, name = "Fora do escopo" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [PostgresFact]
    public async Task TerminatedWorkersCannotBeChiefOrParticipantEvenWhenTheChiefIsUnchanged()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await ClientAsync(factory);
        Guid workerId; Guid teamId;
        await using (var db = database.CreateContext())
        {
            var hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var worker = new Worker(database.CompanyA, "QA-" + hash[..12], [1, 2, 3], hash, "Vínculo encerrado de QA", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, null);
            worker.Terminate(new(2026, 1, 1)); db.Add(worker); workerId = worker.Id;
            var team = new SESMT.Domain.Modules.Organization.Entities.Team(database.CompanyA, database.CompanyA, database.UnitA, "QA-" + hash, null, workerId, []);
            db.Add(team); teamId = team.Id; await db.SaveChangesAsync();
        }
        using var chief = await client.PostAsJsonAsync("/api/v1/teams", new { organizationId = database.CompanyA, companyId = database.CompanyA, localId = database.UnitA, name = "QA-" + Guid.NewGuid().ToString("N"), chiefId = workerId, participantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, chief.StatusCode);
        using var participant = await client.PostAsJsonAsync("/api/v1/teams", Payload("QA-" + Guid.NewGuid().ToString("N"), database.CompanyA, [workerId]));
        Assert.Equal(HttpStatusCode.BadRequest, participant.StatusCode);
        using var unchanged = await client.PatchAsJsonAsync($"/api/v1/teams/{teamId}", new { expectedVersion = 1, name = "QA-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
        await using var verify = database.CreateContext(); Assert.Equal(1, (await verify.Teams.SingleAsync(t => t.Id == teamId)).Version);
    }
}
