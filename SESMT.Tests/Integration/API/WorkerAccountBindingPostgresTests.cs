using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class WorkerAccountBindingPostgresTests(PostgresDatabaseFixture database)
{
    private async Task<(HttpClient Client, Guid Actor)> ClientAsync(PostgresApiFactory factory, bool userEdit = true, bool fullCompany = true)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var actor = new User("Administrador de QA", key + "@example.invalid", "test-only-hash");
        var role = new Role("QA_B_" + key, "Vínculo QA", "Somente banco isolado");
        var scope = new UserScope(actor.Id, fullCompany ? "EMPRESA" : "UNIDADE"); scope.Update(actor.Id, scope.ScopeType, fullCompany ? database.CompanyA : database.UnitA);
        db.AddRange(actor, role, scope, new UserRole(actor.Id, role.Id, database.CompanyA));
        foreach (var (resource, action) in new[] { ("trabalhador", "ler"), ("trabalhador", "editar"), ("usuario", "ler"), ("usuario", "editar") }.Where(p => userEdit || p != ("usuario", "editar")))
        {
            var grant = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == resource && p.Action == action && p.Active && p.DeletedAt == null);
            if (grant is null) { grant = new Permission(resource, action, "Teste"); db.Add(grant); } db.Add(new RolePermission(role.Id, grant.Id));
        }
        await db.SaveChangesAsync(); var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", actor.Id.ToString()); return (client, actor.Id);
    }
    private async Task<Guid> WorkerAsync()
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var worker = new Worker(database.CompanyA, "QA-" + key[..20], [1, 2, 3], key + key, "Pessoa do vínculo", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, null);
        db.Add(worker); await db.SaveChangesAsync(); return worker.Id;
    }
    private async Task<User> AccountAsync(Guid? company = null, bool active = true)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N"); var user = new User("Conta " + key, key + "@example.invalid", "test-only-hash");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", company ?? database.CompanyA); if (!active) user.Deactivate(database.ActorA);
        db.AddRange(user, scope); await db.SaveChangesAsync(); return user;
    }
    private static object Body(User user, int revision = 1, bool confirmation = true) => new { expectedVersion = revision, userId = user.Id, expectedAccountName = user.Name, expectedAccountEmail = user.Email.Value, confirmation };

    [PostgresFact]
    public async Task LinksAnExistingAccountAtomicallyWithMinimalContextAndAudit()
    {
        await using var factory = new PostgresApiFactory(database); var (client, actor) = await ClientAsync(factory); using var owned = client;
        var id = await WorkerAsync(); var user = await AccountAsync();
        using var context = await client.GetAsync($"/api/v1/workers/{id}/account-binding"); Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        var text = await context.Content.ReadAsStringAsync(); Assert.DoesNotContain("cpf", text, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("dateOfBirth", text);
        using var candidates = await client.GetAsync($"/api/v1/workers/{id}/account-binding/candidates?q={user.Email.Value}&page=1"); Assert.Equal(HttpStatusCode.OK, candidates.StatusCode);
        var page = await candidates.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(user.Id, page.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var saved = await client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(user)); Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var result = await saved.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(user.Id, result.GetProperty("userId").GetGuid()); Assert.Equal(2, result.GetProperty("revision").GetInt32());
        await using var db = database.CreateContext(); var worker = await db.Workers.SingleAsync(w => w.Id == id); Assert.Equal(user.Id, worker.UserId);
        var audit = await db.AuditLogs.SingleAsync(a => a.ResourceId == id && a.Action == "ACCOUNT_LINKED"); Assert.Equal(actor, audit.ActorId); Assert.Equal(database.CompanyA, audit.CompanyId); Assert.True(audit.IsIntegrityValid());
    }
    [PostgresFact]
    public async Task ConfirmationRevisionAndDisplayedIdentityAreRequired()
    {
        await using var factory = new PostgresApiFactory(database); var (client, _) = await ClientAsync(factory); using var owned = client;
        var id = await WorkerAsync(); var user = await AccountAsync();
        using var absent = await client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(user, 0)); Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        using var unconfirmed = await client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(user, confirmation: false)); Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        using var changed = await client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", new { expectedVersion = 1, userId = user.Id, expectedAccountName = "Outra pessoa", expectedAccountEmail = user.Email.Value, confirmation = true }); Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        await using var db = database.CreateContext(); Assert.Null((await db.Workers.SingleAsync(w => w.Id == id)).UserId); Assert.False(await db.AuditLogs.AnyAsync(a => a.ResourceId == id && a.Action == "ACCOUNT_LINKED"));
    }
    [PostgresFact]
    public async Task WorkerChangeOrExistingBindingCannotBeOverwritten()
    {
        await using var factory = new PostgresApiFactory(database); var (client, _) = await ClientAsync(factory); using var owned = client;
        var id = await WorkerAsync(); var user = await AccountAsync();
        await using (var db = database.CreateContext()) { var worker = await db.Workers.SingleAsync(w => w.Id == id); worker.ChangeAssignment(null, database.UnitA, null); await db.SaveChangesAsync(); }
        using var stale = await client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(user)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var existing = await client.PutAsJsonAsync($"/api/v1/workers/{database.WorkerA}/account-binding", Body(user)); Assert.Equal(HttpStatusCode.Conflict, existing.StatusCode);
    }
    [PostgresFact]
    public async Task InvalidOrBoundAccountsAreNotListedOrAccepted()
    {
        await using var factory = new PostgresApiFactory(database); var (client, _) = await ClientAsync(factory); using var owned = client; var id = await WorkerAsync();
        var foreign = await AccountAsync(database.CompanyB); var inactive = await AccountAsync(active: false);
        foreach (var user in new[] { foreign, inactive })
        {
            using var candidate = await client.GetAsync($"/api/v1/workers/{id}/account-binding/candidates/{user.Id}"); Assert.Equal(HttpStatusCode.NotFound, candidate.StatusCode);
            using var denied = await client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(user)); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        }
        using var bound = await client.GetAsync($"/api/v1/workers/{id}/account-binding/candidates/{database.ActorA}"); Assert.Equal(HttpStatusCode.NotFound, bound.StatusCode);
        using var foreignWorker = await client.GetAsync($"/api/v1/workers/{database.WorkerB}/account-binding"); Assert.Equal(HttpStatusCode.NotFound, foreignWorker.StatusCode);
    }
    [PostgresFact]
    public async Task EditingWorkersAloneAndUnitOnlyScopeDoNotGrantAccountBinding()
    {
        await using var factory = new PostgresApiFactory(database); var id = await WorkerAsync();
        foreach (var (edit, full) in new[] { (false, true), (true, false) })
        {
            var (client, _) = await ClientAsync(factory, edit, full); using var owned = client;
            using var denied = await client.GetAsync($"/api/v1/workers/{id}/account-binding"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var anonymous = factory.CreateClient(); using var missing = await anonymous.GetAsync($"/api/v1/workers/{id}/account-binding"); Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
    }
    [PostgresFact]
    public async Task TwoWorkersCannotBindTheSameAccountConcurrently()
    {
        await using var factory = new PostgresApiFactory(database); var (client, _) = await ClientAsync(factory); using var owned = client;
        var first = await WorkerAsync(); var second = await WorkerAsync(); var user = await AccountAsync();
        var replies = await Task.WhenAll(client.PutAsJsonAsync($"/api/v1/workers/{first}/account-binding", Body(user)), client.PutAsJsonAsync($"/api/v1/workers/{second}/account-binding", Body(user)));
        try { Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict); } finally { foreach (var response in replies) response.Dispose(); }
        await using var db = database.CreateContext(); Assert.Equal(1, await db.Workers.CountAsync(w => w.UserId == user.Id));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => (a.ResourceId == first || a.ResourceId == second) && a.Action == "ACCOUNT_LINKED"));
    }
    [PostgresFact]
    public async Task TwoAccountsCannotWinTheSameWorkerRevision()
    {
        await using var factory = new PostgresApiFactory(database); var (client, _) = await ClientAsync(factory); using var owned = client;
        var id = await WorkerAsync(); var first = await AccountAsync(); var second = await AccountAsync();
        var replies = await Task.WhenAll(client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(first)), client.PutAsJsonAsync($"/api/v1/workers/{id}/account-binding", Body(second)));
        try { Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict); } finally { foreach (var response in replies) response.Dispose(); }
        await using var db = database.CreateContext(); Assert.Equal(2, (await db.Workers.SingleAsync(w => w.Id == id)).Revision);
    }
    [PostgresFact]
    public async Task ProfessionalChangeLoadedBeforeBindingCannotEraseTheWinningRevision()
    {
        var id = await WorkerAsync(); var user = await AccountAsync(); await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var a = await first.Workers.SingleAsync(w => w.Id == id); var b = await second.Workers.SingleAsync(w => w.Id == id);
        a.LinkUser(user.Id); await first.SaveChangesAsync(); b.ChangeAssignment(null, database.UnitA, null);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
