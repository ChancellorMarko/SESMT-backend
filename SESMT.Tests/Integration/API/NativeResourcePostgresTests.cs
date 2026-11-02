using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class NativeResourcePostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task NativeWorkerScopeIsEnforcedInSqlBeforePageAndDetail()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        await using (var db = database.CreateContext())
        {
            var role = new Role("QA_N_" + Guid.NewGuid().ToString("N"), "Leitura nativa fictícia", "Somente teste isolado");
            var permission = await db.Permissions.SingleAsync(p => p.Resource == "trabalhador" && p.Action == "ler" && p.Active && p.DeletedAt == null);
            db.AddRange(role, new RolePermission(role.Id, permission.Id), new UserRole(database.ActorA, role.Id, database.CompanyA));
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        var foreign = await client.GetAsync($"/api/v1/workers/{database.WorkerB}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var history = await client.GetAsync($"/api/v1/workers/{database.WorkerB}/history");
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        var own = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workers?companyId={database.CompanyA}&pageSize=200");
        Assert.Contains(own.EnumerateArray(), w => w.GetProperty("id").GetGuid() == database.WorkerA);
        Assert.DoesNotContain(own.EnumerateArray(), w => w.GetProperty("id").GetGuid() == database.WorkerB);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/workers?companyId={database.CompanyB}")).StatusCode);
    }
}
