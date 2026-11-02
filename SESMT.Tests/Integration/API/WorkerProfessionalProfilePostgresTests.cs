using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class WorkerProfessionalProfilePostgresTests(PostgresDatabaseFixture database)
{
    private async Task<(HttpClient Client, Guid Worker, Guid Sector, Guid Position)> Setup(PostgresApiFactory factory, bool edit = true, bool sectorOnly = false)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var person = new User("Pessoa da administração fictícia", key + "@example.invalid", "fixture-only-hash");
        var role = new Role("QA_PRO_" + key, "Edição profissional QA", "Somente teste isolado");
        var department = new Department(database.UnitA, "Setor QA " + key, null, null);
        var position = new Position(database.CompanyA, "Cargo QA " + key, null, null);
        var scope = new UserScope(person.Id, sectorOnly ? "SETOR" : "EMPRESA"); scope.Update(person.Id, scope.ScopeType, sectorOnly ? department.Id : database.CompanyA);
        var worker = new Worker(database.CompanyA, "PRO-" + key[..20], [1,2,3], key + key, "Pessoa Profissional Fictícia", new(1990,1,1), new(2020,1,1), null, null, database.UnitA, department.Id, null);
        db.AddRange(person, role, department, position, scope, worker, new UserRole(person.Id, role.Id, database.CompanyA));
        foreach (var action in edit ? new[] { "ler", "editar" } : new[] { "ler" })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == "trabalhador" && p.Action == action && p.Active && p.DeletedAt == null);
            if (permission is null) { permission = new Permission("trabalhador", action, "QA"); db.Add(permission); }
            db.Add(new RolePermission(role.Id, permission.Id));
        }
        await db.SaveChangesAsync(); var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", person.Id.ToString());
        return (client, worker.Id, department.Id, position.Id);
    }
    private static object Body(string name, string enrollment, Guid? sector, Guid? position, int revision = 1, bool confirmation = true) =>
        new { name, enrollment, departmentId = sector, positionId = position, reason = "Correção fictícia solicitada", expectedRevision = revision, confirmation };

    [PostgresFact]
    public async Task UpdatesOnlyProfessionalFieldsAndAppendsHistoryWithAnIntegrityCheckedAudit()
    {
        await using var factory = new PostgresApiFactory(database); var f = await Setup(factory); using var client = f.Client;
        using var read = await client.GetAsync($"/api/v1/workers/{f.Worker}/professional-profile"); Assert.Equal(HttpStatusCode.OK, read.StatusCode); Assert.True(read.Headers.CacheControl?.NoStore);
        var text = await read.Content.ReadAsStringAsync(); Assert.DoesNotContain("cpf", text, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("dateOfBirth", text);
        using var document = JsonDocument.Parse(text); var enrollment = document.RootElement.GetProperty("worker").GetProperty("enrollment").GetString()!;
        using var reply = await client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Pessoa Atualizada", enrollment, f.Sector, f.Position));
        Assert.True(reply.StatusCode == HttpStatusCode.OK, await reply.Content.ReadAsStringAsync());
        await using var db = database.CreateContext(); var stored = await db.Workers.SingleAsync(w => w.Id == f.Worker);
        Assert.Equal(2, stored.Revision); Assert.Equal("Pessoa Atualizada", stored.Name.Value); Assert.Equal(new byte[] { 1,2,3 }, stored.CpfEncryption);
        Assert.Equal(new DateOnly(1990,1,1), stored.DateOfBirth); Assert.Equal(new DateOnly(2020,1,1), stored.AdmissionDate); Assert.Null(stored.UserId);
        Assert.Equal(f.Position, (await db.Set<WorkerHistory>().SingleAsync(h => h.WorkerId == f.Worker && h.EndDate == null)).PositionId);
        Assert.True((await db.AuditLogs.SingleAsync(a => a.ResourceId == f.Worker && a.Action == "PROFESSIONAL_PROFILE_UPDATED")).IsIntegrityValid());
        using var stale = await client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Pessoa Antiga", enrollment, f.Sector, f.Position)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
    [PostgresFact]
    public async Task TwoEditorsCannotOverwriteTheSameWorkerRevision()
    {
        await using var factory = new PostgresApiFactory(database); var f = await Setup(factory); using var client = f.Client;
        await using var db = database.CreateContext(); var enrollment = (await db.Workers.SingleAsync(w => w.Id == f.Worker)).Enrollment;
        var results = await Task.WhenAll(client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Primeira Pessoa", enrollment, f.Sector, f.Position)),
            client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Segunda Pessoa", enrollment, f.Sector, f.Position)));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var result in results) result.Dispose(); }
        Assert.Equal(1, await db.Set<WorkerHistory>().CountAsync(h => h.WorkerId == f.Worker && h.EndDate == null));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.ResourceId == f.Worker && a.Action == "PROFESSIONAL_PROFILE_UPDATED"));
    }
    [PostgresFact]
    public async Task ReadOnlyAndForeignCompanyDoNotExposeTheEditor()
    {
        await using var factory = new PostgresApiFactory(database); var reader = await Setup(factory, edit: false); using var first = reader.Client;
        using var denied = await first.GetAsync($"/api/v1/workers/{reader.Worker}/professional-profile"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var editor = await Setup(factory); using var second = editor.Client;
        using var foreign = await second.GetAsync($"/api/v1/workers/{database.WorkerB}/professional-profile"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
    [PostgresFact]
    public async Task AScopedSectorCannotMoveThePersonToAnotherSectorInTheSameUnit()
    {
        await using var factory = new PostgresApiFactory(database); var f = await Setup(factory, sectorOnly: true); using var client = f.Client;
        await using var db = database.CreateContext(); var other = new Department(database.UnitA, "Outro setor " + Guid.NewGuid().ToString("N"), null, null); db.Add(other); await db.SaveChangesAsync();
        var enrollment = (await db.Workers.SingleAsync(w => w.Id == f.Worker)).Enrollment;
        using var reply = await client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Pessoa Atualizada", enrollment, other.Id, f.Position));
        Assert.Equal(HttpStatusCode.Forbidden, reply.StatusCode); db.ChangeTracker.Clear();
        Assert.Equal(f.Sector, (await db.Workers.SingleAsync(w => w.Id == f.Worker)).DepartmentId);
        Assert.Empty(await db.Set<WorkerHistory>().Where(h => h.WorkerId == f.Worker).ToListAsync());
    }
    [PostgresFact]
    public async Task AConflictOrMissingConfirmationDoesNotWriteProfessionalChanges()
    {
        await using var factory = new PostgresApiFactory(database); var f = await Setup(factory); using var client = f.Client;
        await using var db = database.CreateContext(); var before = await db.Workers.SingleAsync(w => w.Id == f.Worker);
        using var refused = await client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Pessoa Alterada", before.Enrollment, f.Sector, f.Position, confirmation: false)); Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        using var conflict = await client.PatchAsJsonAsync($"/api/v1/workers/{f.Worker}/professional-profile", Body("Pessoa Alterada", before.Enrollment, f.Sector, f.Position, revision: 9)); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await db.Entry(before).ReloadAsync(); Assert.Equal(1, before.Revision); Assert.Empty(await db.Set<WorkerHistory>().Where(h => h.WorkerId == f.Worker).ToListAsync());
    }
}
