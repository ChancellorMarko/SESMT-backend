using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class EquipmentPostgresTests(PostgresDatabaseFixture database)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    private async Task<HttpClient> Admin(PostgresApiFactory factory)
    {
        var client = factory.CreateClient(); await using var db = database.CreateContext();
        var id = await db.Users.Where(u => u.Email.Value == "seed-admin@example.invalid").Select(u => u.Id).SingleAsync();
        client.DefaultRequestHeaders.Add("X-Test-Actor", id.ToString()); return client;
    }
    private async Task<(HttpClient Client, Guid Actor)> Scoped(PostgresApiFactory factory, bool write = false, bool ownOnly = false)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var person = new User("Pessoa da equipe de teste", key + "@example.invalid", "fixture-only-hash");
        var role = ownOnly ? await db.Roles.SingleAsync(r => r.Code == "TRABALHADOR") : new Role("QA_EPI_" + key, "Equipe de teste", "Acesso isolado de teste");
        var scope = new UserScope(person.Id, "EMPRESA"); scope.Update(person.Id, "EMPRESA", database.CompanyA);
        db.AddRange(person, scope, new UserRole(person.Id, role.Id, database.CompanyA));
        if (!ownOnly)
        {
            db.Add(role);
            foreach (var action in write ? new[] { "ler", "criar", "editar" } : new[] { "ler" })
                db.Add(new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "epi" && p.Action == action)).Id));
            db.Add(new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "trabalhador" && p.Action == "ler")).Id));
        }
        else db.Add(new Worker(database.CompanyA, "OWN-" + key[..20], [1,2,3], key + key, "Pessoa com acesso próprio", new(1990,1,1), new(2020,1,1), null, null, database.UnitA, null, person.Id));
        await db.SaveChangesAsync(); var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", person.Id.ToString()); return (client, person.Id);
    }
    private async Task<JsonElement> Create(HttpClient client, Guid? unit = null, string? code = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/equipment", new { unitId = unit ?? database.UnitA, code = code ?? "EPI-" + Guid.NewGuid().ToString("N")[..15], name = "Luva de proteção", approvalCertificate = "12345", manufacturer = "Fabricante informado", model = "Modelo cadastrado", confirmation = true });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    private async Task<JsonElement> Deliver(HttpClient client, Guid equipment, Guid? worker = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/equipment/deliveries", new { unitId = database.UnitA, workerId = worker ?? database.WorkerA,
            equipmentId = equipment, commandId = Guid.NewGuid(), quantity = 2, size = "M", deliveredOn = Today, replacementOn = Today.AddDays(60), notes = "Registro de entrega", confirmation = true });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    [PostgresFact]
    public async Task CatalogDeliveryAndReturnPersistWithVersionAndAudit()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory);
        using var context = await client.GetAsync($"/api/v1/equipment/context?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        var equipment = await Create(client); var id = equipment.GetProperty("id").GetGuid(); var delivery = await Deliver(client, id); var deliveryId = delivery.GetProperty("id").GetGuid();
        using var search = await client.GetAsync($"/api/v1/equipment/deliveries?unitId={database.UnitA}&q=Luva&pageSize=20"); Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var edit = await client.PutAsJsonAsync($"/api/v1/equipment/{id}", new { unitId = database.UnitA, code = equipment.GetProperty("code").GetString(), name = "Luva atualizada", approvalCertificate = "54321", confirmation = true, expectedRevision = 1 }); Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using var historical = await client.GetAsync($"/api/v1/equipment/deliveries/{deliveryId}?unitId={database.UnitA}"); var history = await historical.Content.ReadAsStringAsync(); Assert.Contains("Luva de prote", history); Assert.Contains("12345", history);
        using var returned = await client.PostAsJsonAsync($"/api/v1/equipment/deliveries/{deliveryId}/return", new { unitId = database.UnitA, expectedRevision = 1, returnedOn = Today, reason = "Substituição por desgaste", confirmation = true }); Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        using var duplicate = await client.PostAsJsonAsync($"/api/v1/equipment/deliveries/{deliveryId}/return", new { unitId = database.UnitA, expectedRevision = 1, returnedOn = Today, reason = "Repetição", confirmation = true }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var db = database.CreateContext(); var stored = await db.EquipmentDeliveries.SingleAsync(d => d.Id == deliveryId); Assert.Equal(2, stored.Revision); Assert.Equal(Today, stored.ReturnedOn);
        Assert.All(await db.AuditLogs.Where(a => a.ResourceId == deliveryId && a.Action.StartsWith("EQUIPMENT_")).ToArrayAsync(), a => Assert.True(a.IsIntegrityValid()));
    }
    [PostgresFact]
    public async Task OwnOnlyListsOnlyOwnDeliveriesAndNeverAllowsWriting()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory); var own = await Scoped(factory, ownOnly:true); using var client = own.Client;
        await using var db = database.CreateContext(); var worker = await db.Workers.SingleAsync(w => w.UserId == own.Actor);
        using var identityResponse = await client.GetAsync("/api/v1/auth/me"); Assert.Equal(HttpStatusCode.OK, identityResponse.StatusCode);
        using var identity = JsonDocument.Parse(await identityResponse.Content.ReadAsStringAsync());
        Assert.True(identity.RootElement.GetProperty("scope").GetProperty("ownOnly").GetBoolean());
        Assert.Equal(worker.Id, identity.RootElement.GetProperty("workerId").GetGuid());
        Assert.Contains(identity.RootElement.GetProperty("permissions").EnumerateArray(), permission => permission.GetString() == "epi:ler");
        var equipment = await Create(admin); var id = equipment.GetProperty("id").GetGuid(); var mine = await Deliver(admin, id, worker.Id); var other = await Deliver(admin, id);
        using var response = await client.GetAsync($"/api/v1/equipment/deliveries?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32()); Assert.Equal(mine.GetProperty("id").GetGuid(), body.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var foreign = await client.GetAsync($"/api/v1/equipment/deliveries/{other.GetProperty("id").GetGuid()}?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var write = await client.PostAsJsonAsync("/api/v1/equipment",new {unitId=database.UnitA,code="DENIED",name="Equipamento",confirmation=true}); Assert.Equal(HttpStatusCode.Forbidden,write.StatusCode);
    }
    [PostgresFact]
    public async Task CompanyScopeAndReadOnlyGrantsAreEnforcedOnEveryCommand()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory); var scoped = await Scoped(factory, write:true); using var client = scoped.Client;
        var other = await Create(admin, database.UnitB); var otherId=other.GetProperty("id").GetGuid();
        using var outside = await client.GetAsync($"/api/v1/equipment?unitId={database.UnitB}"); Assert.Equal(HttpStatusCode.Forbidden,outside.StatusCode);
        using var tampered = await client.PostAsJsonAsync("/api/v1/equipment/deliveries", new { unitId=database.UnitA,workerId=database.WorkerA,equipmentId=otherId,commandId=Guid.NewGuid(),quantity=1,deliveredOn=Today,confirmation=true }); Assert.Equal(HttpStatusCode.NotFound,tampered.StatusCode);
        var reader = await Scoped(factory); using var readClient = reader.Client; using var denied = await readClient.PostAsJsonAsync("/api/v1/equipment",new {unitId=database.UnitA,code="DENIED",name="Equipamento",confirmation=true}); Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var anonymous = factory.CreateClient(); using var unauthenticated = await anonymous.GetAsync($"/api/v1/equipment?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.Unauthorized,unauthenticated.StatusCode);
    }
    [PostgresFact]
    public async Task DuplicateCodesCommandsAndInvalidDatesCannotCreateExtraRows()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var equipment=await Create(client); var id=equipment.GetProperty("id").GetGuid();
        using var duplicate = await client.PostAsJsonAsync("/api/v1/equipment",new {unitId=database.UnitA,code=equipment.GetProperty("code").GetString()!.ToLower(),name="Duplicado",confirmation=true}); Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        var command=Guid.NewGuid(); var body=new {unitId=database.UnitA,workerId=database.WorkerA,equipmentId=id,commandId=command,quantity=1,deliveredOn=Today,confirmation=true};
        using var first=await client.PostAsJsonAsync("/api/v1/equipment/deliveries",body); Assert.Equal(HttpStatusCode.Created,first.StatusCode);
        using var second=await client.PostAsJsonAsync("/api/v1/equipment/deliveries",body); Assert.Equal(HttpStatusCode.Conflict,second.StatusCode);
        using var future=await client.PostAsJsonAsync("/api/v1/equipment/deliveries",new {unitId=database.UnitA,workerId=database.WorkerA,equipmentId=id,commandId=Guid.NewGuid(),quantity=1,deliveredOn=Today.AddDays(1),confirmation=true}); Assert.Equal(HttpStatusCode.BadRequest,future.StatusCode);
        await using var db=database.CreateContext(); Assert.Equal(1,await db.EquipmentDeliveries.CountAsync(d=>d.EquipmentId==id));
    }
    [PostgresFact]
    public async Task TwoReturnsCannotOverwriteTheSameRevision()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var item=await Create(client); var delivery=await Deliver(client,item.GetProperty("id").GetGuid()); var id=delivery.GetProperty("id").GetGuid();
        var body=new {unitId=database.UnitA,expectedRevision=1,returnedOn=Today,reason="Devolução conferida",confirmation=true};
        var replies=await Task.WhenAll(client.PostAsJsonAsync($"/api/v1/equipment/deliveries/{id}/return",body),client.PostAsJsonAsync($"/api/v1/equipment/deliveries/{id}/return",body));
        try { Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.OK); Assert.Single(replies,r=>r.StatusCode==HttpStatusCode.Conflict); } finally {foreach(var reply in replies)reply.Dispose();}
    }
}
