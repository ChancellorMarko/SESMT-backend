using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class TrainingPostgresTests(PostgresDatabaseFixture database)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    private async Task<HttpClient> Admin(PostgresApiFactory factory)
    {
        await using var db = database.CreateContext(); var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (await db.Users.SingleAsync(u => u.Email.Value == "seed-admin@example.invalid")).Id.ToString()); return client;
    }
    private object Body(Guid? worker = null, Guid? command = null, string title = "Integração de segurança", int? revision = null, int duration = 90,
        DateOnly? completed = null, DateOnly? renewal = null) => new { unitId = database.UnitA, workerId = worker ?? database.WorkerA,
            commandId = command ?? Guid.NewGuid(), title, reference = "Procedimento interno", kind = "INITIAL", durationMinutes = duration,
            completedOn = completed ?? Today, renewalOn = renewal, confirmation = true, expectedRevision = revision, changeReason = "Correção conferida pela equipe" };
    private async Task<JsonElement> Create(HttpClient client, Guid? worker = null, string title = "Integração de segurança")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/trainings", Body(worker, title: title));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    private async Task<(HttpClient Client, Guid Actor)> Scoped(PostgresApiFactory factory, bool own = false)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var user = new User("Pessoa de treinamento", key + "@example.invalid", "fixture-only-hash");
        var role = own ? await db.Roles.SingleAsync(r => r.Code == "TRABALHADOR") : new Role("QA_TRAINING_" + key[..20], "Consulta de cursos", "Consulta isolada de teste");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, scope, new UserRole(user.Id, role.Id, database.CompanyA));
        if (!own) { db.Add(role); db.Add(new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "treinamento" && p.Action == "ler")).Id)); }
        else db.Add(new Worker(database.CompanyA, "TRAIN-" + key[..20], [1, 2, 3], key + key, "Pessoa com cursos próprios", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, user.Id));
        await db.SaveChangesAsync(); var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString()); return (client, user.Id);
    }

    [PostgresFact]
    public async Task CreatesEditsAndInvalidatesWithoutDeletingTheRecordOrItsAudit()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory);
        var created = await Create(client); var id = created.GetProperty("id").GetGuid();
        using var edited = await client.PutAsJsonAsync($"/api/v1/trainings/{id}", Body(title: "Integração de segurança atualizada", revision: 1)); Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var stale = await client.PutAsJsonAsync($"/api/v1/trainings/{id}", Body(revision: 1)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalidated = await client.PostAsJsonAsync($"/api/v1/trainings/{id}/revoke", new { unitId = database.UnitA, expectedRevision = 2, reason = "Registro incluído para a pessoa errada", confirmation = true }); Assert.Equal(HttpStatusCode.OK, invalidated.StatusCode);
        using var again = await client.PutAsJsonAsync($"/api/v1/trainings/{id}", Body(revision: 3)); Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using var history = await client.GetAsync($"/api/v1/trainings/{id}/history?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var text = await history.Content.ReadAsStringAsync(); using var events = JsonDocument.Parse(text);
        Assert.Equal(3, events.RootElement.GetArrayLength()); Assert.Equal(3, events.RootElement[0].GetProperty("revision").GetInt32());
        Assert.Equal("Registro incluído para a pessoa errada", events.RootElement[0].GetProperty("reason").GetString());
        Assert.DoesNotContain("email", text, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("ipAddress", text);
        await using var db = database.CreateContext(); var stored = await db.TrainingRecords.SingleAsync(t => t.Id == id);
        Assert.True(stored.Active); Assert.Null(stored.DeletedAt); Assert.NotNull(stored.RevokedAt); Assert.Equal(3, stored.Revision);
        Assert.All(await db.AuditLogs.Where(a => a.ResourceId == id && a.ResourceType == "treinamento").ToArrayAsync(), a => Assert.True(a.IsIntegrityValid()));
    }

    [PostgresFact]
    public async Task OwnOnlyReadsOnlyOwnCoursesAndCannotRegisterEditOrInvalidate()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory); var own = await Scoped(factory, true); using var client = own.Client;
        await using var db = database.CreateContext(); var worker = await db.Workers.SingleAsync(w => w.UserId == own.Actor);
        var mine = await Create(admin, worker.Id); var foreign = await Create(admin);
        using var list = await client.GetAsync($"/api/v1/trainings?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var parsed = JsonDocument.Parse(await list.Content.ReadAsStringAsync()); Assert.Equal(1, parsed.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(mine.GetProperty("id").GetGuid(), parsed.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var other = await client.GetAsync($"/api/v1/trainings/{foreign.GetProperty("id").GetGuid()}/history?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var create = await client.PostAsJsonAsync("/api/v1/trainings", Body(worker.Id)); Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var edit = await client.PutAsJsonAsync($"/api/v1/trainings/{mine.GetProperty("id").GetGuid()}", Body(worker.Id, revision: 1)); Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        using var revoke = await client.PostAsJsonAsync($"/api/v1/trainings/{mine.GetProperty("id").GetGuid()}/revoke", new { unitId = database.UnitA, expectedRevision = 1, reason = "Tentativa recusada", confirmation = true }); Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
    }

    [PostgresFact]
    public async Task ReadPermissionAndCompanyScopeDoNotGrantWritingOrForeignRecords()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory); var reader = await Scoped(factory); using var client = reader.Client;
        using var allowed = await client.GetAsync($"/api/v1/trainings?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var outside = await client.GetAsync($"/api/v1/trainings?unitId={database.UnitB}"); Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
        using var denied = await client.PostAsJsonAsync("/api/v1/trainings", Body()); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var tampered = await admin.PostAsJsonAsync("/api/v1/trainings", Body(database.WorkerB)); Assert.Equal(HttpStatusCode.UnprocessableEntity, tampered.StatusCode);
        using var anonymous = factory.CreateClient(); using var noSession = await anonymous.GetAsync($"/api/v1/trainings?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
    }

    [PostgresFact]
    public async Task DuplicatesInvalidDatesAndMissingPreconditionsNeverCreateExtraRecords()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var command = Guid.NewGuid();
        using var first = await client.PostAsJsonAsync("/api/v1/trainings", Body(command: command)); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/trainings", Body(command: command)); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var future = await client.PostAsJsonAsync("/api/v1/trainings", Body(completed: Today.AddDays(1))); Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        using var earlyRenewal = await client.PostAsJsonAsync("/api/v1/trainings", Body(renewal: Today.AddDays(-1))); Assert.Equal(HttpStatusCode.BadRequest, earlyRenewal.StatusCode);
        using var zero = await client.PostAsJsonAsync("/api/v1/trainings", Body(duration: 0)); Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        var id = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        using var missingRevision = await client.PutAsJsonAsync($"/api/v1/trainings/{id}", Body()); Assert.Equal(HttpStatusCode.BadRequest, missingRevision.StatusCode);
        await using var db = database.CreateContext(); Assert.Equal(1, await db.TrainingRecords.CountAsync(t => t.CommandId == command));
    }

    [PostgresFact]
    public async Task ConcurrentEditsCannotOverwriteTheWinningRevision()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var item = await Create(client); var id = item.GetProperty("id").GetGuid();
        var replies = await Task.WhenAll(client.PutAsJsonAsync($"/api/v1/trainings/{id}", Body(title: "Primeira correção", revision: 1)), client.PutAsJsonAsync($"/api/v1/trainings/{id}", Body(title: "Segunda correção", revision: 1)));
        try { Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var reply in replies) reply.Dispose(); }
        await using var db = database.CreateContext(); Assert.Equal(2, (await db.TrainingRecords.SingleAsync(t => t.Id == id)).Revision);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.ResourceId == id && a.ResourceType == "treinamento"));
    }

    [PostgresFact]
    public async Task RenewalFiltersAreAppliedBeforeCountAndPagination()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); await using var db = database.CreateContext();
        var marker = "CURSO-" + Guid.NewGuid().ToString("N")[..10];
        for (var i = 0; i < 25; i++) {
            var record = new TrainingRecord(database.CompanyA, database.UnitA, database.WorkerA, Guid.NewGuid(), database.ActorA);
            record.Set(marker + i, null, "INITIAL", Today.AddDays(-60), i < 22 ? Today.AddDays(-1) : Today.AddDays(60), 60, null, null, null, null, null); db.Add(record);
        }
        await db.SaveChangesAsync();
        using var response = await client.GetAsync($"/api/v1/trainings?unitId={database.UnitA}&q={marker}&status=EXPIRED&page=2&pageSize=20"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal(22, body.RootElement.GetProperty("total").GetInt32()); Assert.Equal(2, body.RootElement.GetProperty("items").GetArrayLength());
        using var bad = await client.GetAsync($"/api/v1/trainings?unitId={database.UnitA}&status=UNKNOWN"); Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
