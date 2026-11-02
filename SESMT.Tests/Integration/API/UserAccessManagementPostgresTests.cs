using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class UserAccessManagementPostgresTests(PostgresDatabaseFixture database)
{
    private async Task<string> AllowManagementAsync()
    {
        await using var db = database.CreateContext();
        var role = new Role("QA_ACCESS_" + Guid.NewGuid().ToString("N")[..24], "Gestão de contas fictícia", "Somente teste isolado");
        db.AddRange(role, new UserRole(database.ActorA, role.Id, database.CompanyA));
        foreach (var action in new[] { "ler", "criar", "editar" })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == "usuario" && p.Action == action && p.Active && p.DeletedAt == null);
            if (permission is null) { permission = new Permission("usuario", action, "Teste isolado"); db.Add(permission); }
            db.Add(new RolePermission(role.Id, permission.Id));
        }
        await db.SaveChangesAsync(); return role.Code;
    }
    private async Task<Guid> CreateTarget(HttpClient client, string role)
    {
        using var reply = await client.PostAsJsonAsync("/api/v1/users", new { name = "Conta fictícia do teste", email = $"qa-{Guid.NewGuid():N}@example.invalid", roles = new[] { role }, scopeIds = new[] { database.CompanyA } });
        var text = await reply.Content.ReadAsStringAsync(); Assert.True(reply.StatusCode == HttpStatusCode.Created, text);
        using var parsed = JsonDocument.Parse(text); return parsed.RootElement.GetProperty("id").GetGuid();
    }

    [PostgresFact]
    public async Task PublishesDelegableCatalogAndCreatesARealScopedAccountWithoutExposingPassword()
    {
        await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
        var code = await AllowManagementAsync(); client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var reply = await client.GetAsync("/api/v1/users/management-context?action=criar");
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode); using var body = JsonDocument.Parse(await reply.Content.ReadAsStringAsync());
        Assert.All(body.RootElement.GetProperty("scopes").EnumerateArray(), scope => Assert.Equal(database.CompanyA, scope.GetProperty("companyId").GetGuid()));
        Assert.Contains(body.RootElement.GetProperty("roles").EnumerateArray(), role => role.GetProperty("code").GetString() == code);
        var id = await CreateTarget(client, code);
        await using var db = database.CreateContext();
        Assert.True((await db.Users.SingleAsync(u => u.Id == id)).Active);
        Assert.Equal(database.CompanyA, (await db.UserRoles.SingleAsync(r => r.UserId == id && r.DeletedAt == null)).CompanyId);
        using var list = await client.GetAsync("/api/v1/users"); var listText = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", listText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(id.ToString(), listText);
    }

    [PostgresFact]
    public async Task TwoEditorsWithTheSameVersionCannotOverwriteEachOther()
    {
        await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
        var firstRole = await AllowManagementAsync(); var secondRole = await AllowManagementAsync();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        var id = await CreateTarget(client, firstRole);
        using var context = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/users/management-context?action=editar&userId={id}"));
        var version = context.RootElement.GetProperty("accessVersion").GetString();
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/v1/users/{id}/access", new { roles = new[] { firstRole }, scopeIds = new[] { database.UnitA }, expectedVersion = version, confirmation = true }),
            client.PutAsJsonAsync($"/api/v1/users/{id}/access", new { roles = new[] { secondRole }, scopeIds = new[] { database.CompanyA }, expectedVersion = version, confirmation = true }));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            using var winner = JsonDocument.Parse(await responses.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadAsStringAsync());
            await using var db = database.CreateContext();
            var expectedScope = winner.RootElement.GetProperty("user").GetProperty("scopeIds")[0].GetGuid();
            Assert.Equal(expectedScope, (await db.UserScopes.SingleAsync(s => s.UserId == id && s.DeletedAt == null)).ReferenceId);
            Assert.Single(await db.UserRoles.Where(r => r.UserId == id && r.DeletedAt == null).ToListAsync());
            Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.ResourceId == id && log.Action == "ALTERACAO_PERMISSAO"));
            Assert.True((await db.AuditLogs.SingleAsync(log => log.ResourceId == id && log.Action == "ALTERACAO_PERMISSAO")).IsIntegrityValid());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task ForeignScopeAndMissingConfirmationDoNotChangeTheAccount()
    {
        await using var factory = new PostgresApiFactory(database); using var client = factory.CreateClient();
        var role = await AllowManagementAsync(); client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString()); var id = await CreateTarget(client, role);
        using var context = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/users/management-context?action=editar&userId={id}"));
        var version = context.RootElement.GetProperty("accessVersion").GetString();
        using var denied = await client.PutAsJsonAsync($"/api/v1/users/{id}/access", new { roles = new[] { role }, scopeIds = new[] { database.CompanyB }, expectedVersion = version, confirmation = true });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var missing = await client.PutAsJsonAsync($"/api/v1/users/{id}/access", new { roles = new[] { role }, scopeIds = new[] { database.UnitA }, expectedVersion = version, confirmation = false });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(database.CompanyA, (await db.UserScopes.SingleAsync(s => s.UserId == id && s.DeletedAt == null)).ReferenceId);
        Assert.Equal(0, await db.AuditLogs.CountAsync(log => log.ResourceId == id && log.Action == "ALTERACAO_PERMISSAO"));
    }
}
