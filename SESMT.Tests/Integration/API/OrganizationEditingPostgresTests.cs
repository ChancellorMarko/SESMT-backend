using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class OrganizationEditingPostgresTests(PostgresDatabaseFixture database)
{
    private const string Root = "/api/v1/organization-management";
    private async Task<HttpClient> Admin(PostgresApiFactory factory) {
        var client = factory.CreateClient(); await using var db = database.CreateContext();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (await db.Users.SingleAsync(u => u.Email.Value == "seed-admin@example.invalid")).Id.ToString()); return client;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK) {
        var text = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == status,text); return JsonDocument.Parse(text).RootElement.Clone();
    }
    private static object Body(JsonElement state, Action<JsonObject>? change = null) {
        var values = JsonNode.Parse(state.GetProperty("values").GetRawText())!.AsObject(); change?.Invoke(values);
        return new { expectedVersion = state.GetProperty("version").GetString(), reason = "Correção conferida no cadastro", confirmation = true, values };
    }
    private async Task<(Guid Sector,Guid Position)> Setup() {
        await using var db = database.CreateContext(); var tag = Guid.NewGuid().ToString("N")[..8];
        var d = new Department(database.UnitA,"Setor " + tag,"Descrição original",database.ActorA);
        var p = new Position(database.CompanyA,"Cargo " + tag,"123456","Descrição original"); db.AddRange(d,p); await db.SaveChangesAsync(); return (d.Id,p.Id);
    }
    [PostgresFact]
    public async Task AllFourResourcesEditRereadAndKeepAuditWithTheSamePersistedVersion() {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var setup = await Setup();
        foreach (var (resource,id) in new[] { ("companies",database.CompanyA),("units",database.UnitA),("departments",setup.Sector),("positions",setup.Position) }) {
            var path = $"{Root}/{resource}/{id}"; using var get = await client.GetAsync(path); var original = await Json(get);
            var name = "Cadastro " + Guid.NewGuid().ToString("N")[..10];
            using var edit = await client.PatchAsJsonAsync(path,Body(original,v => v[resource == "companies" ? "fantasyName" : "name"] = name)); var saved = await Json(edit);
            Assert.NotEqual(original.GetProperty("version").GetString(),saved.GetProperty("version").GetString());
            using var read = await client.GetAsync(path); Assert.Equal(saved.ToString(),(await Json(read)).ToString());
            using var history = await client.GetAsync(path + "/history"); var events = await Json(history); Assert.True(events.GetArrayLength() > 0);
            Assert.Equal(saved.GetProperty("version").GetString(),events[0].GetProperty("version").GetString());
            using var stale = await client.PatchAsJsonAsync(path,Body(original)); Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
            await using var db = database.CreateContext(); Assert.All(await db.AuditLogs.Where(a => a.ResourceId == id && a.ResourceType == "organizacao").ToArrayAsync(),a=>Assert.True(a.IsIntegrityValid()));
        }
        await using var stored = database.CreateContext(); Assert.Equal(database.ActorA,(await stored.Departments.SingleAsync(d=>d.Id==setup.Sector)).ResponsibleId);
    }
    [PostgresFact]
    public async Task ConcurrentEditsFromTheSameVersionProduceOneSuccessAndOneConflict() {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var setup = await Setup(); var path=$"{Root}/positions/{setup.Position}";
        using var read=await client.GetAsync(path); var state=await Json(read);
        var responses=await Task.WhenAll(client.PatchAsJsonAsync(path,Body(state,v=>v["name"]="Primeira edição")),client.PatchAsJsonAsync(path,Body(state,v=>v["name"]="Segunda edição")));
        try { Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK); Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict); }
        finally { foreach(var r in responses)r.Dispose(); }
        await using var db=database.CreateContext(); Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.ResourceId==setup.Position && a.ResourceType=="organizacao"));
    }
    [PostgresFact]
    public async Task LegacyPatchCannotBypassTheVersionAndInvalidatesOldEditorReceipts() {
        await using var factory=new PostgresApiFactory(database); using var client=await Admin(factory);var setup=await Setup(); var path=$"{Root}/positions/{setup.Position}";
        using var read=await client.GetAsync(path);var state=await Json(read);var legacy=$"/api/v1/positions/{setup.Position}";
        using var missing=await client.PatchAsJsonAsync(legacy,new {name="Cargo legado",description="Descrição",cbo="123456"});Assert.Equal((HttpStatusCode)428,missing.StatusCode);
        using var command=new HttpRequestMessage(HttpMethod.Patch,legacy){Content=JsonContent.Create(new {name="Cargo legado corrigido",description="Descrição",cbo="123456"})};
        command.Headers.TryAddWithoutValidation("If-Match",$"\"{state.GetProperty("version").GetString()}\"");using var saved=await client.SendAsync(command);await Json(saved);
        using var old=await client.PatchAsJsonAsync(path,Body(state));Assert.Equal(HttpStatusCode.Conflict,old.StatusCode);
        using var staleCommand=new HttpRequestMessage(HttpMethod.Patch,legacy){Content=JsonContent.Create(new {name="Sobrescrita",cbo="123456"})};
        staleCommand.Headers.TryAddWithoutValidation("If-Match",$"\"{state.GetProperty("version").GetString()}\"");using var stale=await client.SendAsync(staleCommand);Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
    }
    [PostgresFact]
    public async Task ImmutableFieldsUnknownFieldsAndOversizedDataNeverChangeTheRecord() {
        await using var factory=new PostgresApiFactory(database);using var client=await Admin(factory);var path=$"{Root}/companies/{database.CompanyA}";
        using var read=await client.GetAsync(path);var state=await Json(read);
        foreach(var change in new Action<JsonObject>[] {v=>v["cnpj"]="11444777000161",v=>v["tipoEmpresa"]="INTERNA",v=>v["active"]=false,v=>v["phone"]=new string('1',21)}) {
            using var response=await client.PatchAsJsonAsync(path,Body(state,change));Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        }
        var body=JsonSerializer.SerializeToNode(Body(state))!.AsObject();body["confirmation"]=false;
        using var unconfirmed=await client.PatchAsJsonAsync(path,body);Assert.Equal(HttpStatusCode.BadRequest,unconfirmed.StatusCode);
        using var after=await client.GetAsync(path);Assert.Equal(state.ToString(),(await Json(after)).ToString());
        using var legacyCommand=new HttpRequestMessage(HttpMethod.Patch,$"/api/v1/companies/{database.CompanyA}") { Content=JsonContent.Create(new {corporateName="Troca recusada",fantasyName="Troca recusada",cnpj="12345678000195",tipoEmpresa=1}) };
        legacyCommand.Headers.TryAddWithoutValidation("If-Match",$"\"{state.GetProperty("version").GetString()}\"");using var legacy=await client.SendAsync(legacyCommand);Assert.Equal(HttpStatusCode.BadRequest,legacy.StatusCode);
        using var afterLegacy=await client.GetAsync(path);Assert.Equal(state.ToString(),(await Json(afterLegacy)).ToString());
        var setup=await Setup();using var departmentRead=await client.GetAsync($"{Root}/departments/{setup.Sector}");var department=await Json(departmentRead);
        using var moved=await client.PatchAsJsonAsync($"{Root}/departments/{setup.Sector}",Body(department,v=>v["unitId"]=database.UnitB.ToString()));Assert.Equal(HttpStatusCode.BadRequest,moved.StatusCode);
    }
    [PostgresFact]
    public async Task DuplicateNamesRollBackTheChangeAndItsAudit() {
        await using var factory=new PostgresApiFactory(database);using var client=await Admin(factory);var a=await Setup();var b=await Setup();var path=$"{Root}/positions/{a.Position}";
        using var aRead=await client.GetAsync(path);var original=await Json(aRead);using var bRead=await client.GetAsync($"{Root}/positions/{b.Position}");var other=await Json(bRead);
        using var duplicate=await client.PatchAsJsonAsync(path,Body(original,v=>v["name"]=other.GetProperty("values").GetProperty("name").GetString()));Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        using var after=await client.GetAsync(path);Assert.Equal(original.ToString(),(await Json(after)).ToString());
        await using var db=database.CreateContext();Assert.Equal(0,await db.AuditLogs.CountAsync(a=>a.ResourceId==original.GetProperty("id").GetGuid() && a.ResourceType=="organizacao"));
    }
    [PostgresFact]
    public async Task WholeUnitCanEditOnlyItsUnitAndDepartmentsWhileSectorAndReaderCannotEdit() {
        await using var factory=new PostgresApiFactory(database);using var admin=await Admin(factory);var setup=await Setup();await using var db=database.CreateContext();
        async Task<HttpClient> Scoped(string type,Guid location,bool edit) {
            var key=Guid.NewGuid().ToString("N");var user=new User("Consulta de estrutura",key+"@example.invalid","fixture-only");var role=new Role("ORG_"+key[..20],"Estrutura restrita","Fixture");var scope=new UserScope(user.Id,type);scope.Update(user.Id,type,location);
            db.AddRange(user,role,scope,new UserRole(user.Id,role.Id,database.CompanyA));
            foreach(var p in await db.Permissions.Where(p=>new[]{"empresa","unidade","setor","cargo"}.Contains(p.Resource) && (p.Action=="ler" || edit && p.Action=="editar")).ToArrayAsync())db.Add(new RolePermission(role.Id,p.Id));
            await db.SaveChangesAsync();var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Actor",user.Id.ToString());return client;
        }
        using var unit=await Scoped("UNIDADE",database.UnitA,true);using var sector=await Scoped("SETOR",setup.Sector,true);using var reader=await Scoped("EMPRESA",database.CompanyA,false);
        foreach(var (resource,id) in new[]{("companies",database.CompanyA),("units",database.UnitA),("departments",setup.Sector),("positions",setup.Position)}) {
            using var u=await unit.GetAsync($"{Root}/{resource}/{id}");Assert.Equal(resource is "units" or "departments"?HttpStatusCode.OK:HttpStatusCode.Forbidden,u.StatusCode);
            using var s=await sector.GetAsync($"{Root}/{resource}/{id}");Assert.Equal(HttpStatusCode.Forbidden,s.StatusCode);
            using var r=await reader.GetAsync($"{Root}/{resource}/{id}");Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);
        }
        using var foreign=await unit.GetAsync($"{Root}/units/{database.UnitB}");Assert.Equal(HttpStatusCode.Forbidden,foreign.StatusCode);
    }
}
