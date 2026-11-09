using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class OccupationalHealthPostgresTests(PostgresDatabaseFixture database)
{
    private const string Path = "/api/v1/occupational-health";
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);
    private async Task<(HttpClient Client, Guid? Worker)> Account(PostgresApiFactory factory, string code, string? scopeType = null, Guid? scopeId = null)
    {
        var client = factory.CreateClient();
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var user = new User("Pessoa ASO " + key[..8], key + "@example.invalid", "fixture-only-hash");
        var role = await db.Roles.SingleAsync(r => r.Code == code);
        var scope = new UserScope(user.Id, scopeType ?? "EMPRESA"); scope.Update(user.Id, scopeType ?? "EMPRESA", scopeId ?? database.CompanyA);
        db.AddRange(user, scope, new UserRole(user.Id, role.Id, database.CompanyA)); Guid? worker = null;
        if (code == "TRABALHADOR") { var w = new Worker(database.CompanyA, "ASO-" + key[..20], [1, 2, 3], key + key, "Pessoa ASO " + string.Concat(key.Select(c => char.IsDigit(c) ? (char)('g' + c - '0') : c)), new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, user.Id); db.Add(w); worker = w.Id; }
        await db.SaveChangesAsync(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString()); return (client, worker);
    }
    private object Body(Guid? worker = null, Guid? command = null, int? revision = null, DateOnly? exam = null, string fitness = "UNFIT", DateOnly? next = null)
        => new { unitId = database.UnitA, workerId = worker ?? database.WorkerA, commandId = command ?? Guid.NewGuid(), examKind = "PERIODIC", examOn = exam ?? Today,
            issuedOn = Today, nextEvaluationOn = next, details = new { fitness, evaluatedFunction = "Função administrativa QA", physicianName = "Emitente restrito QA", physicianCrm = "987654", physicianState = "PR", reference = "ASO-QA" },
            confirmation = true, expectedRevision = revision, changeCode = "REGISTRATION_ERROR" };
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status)
    { var text = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == status, text); return JsonDocument.Parse(text).RootElement.Clone(); }

    [PostgresFact]
    public async Task MedicalWorkflowUsesRevisionsAndEncryptsDetailsAndAudit()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var client = doctor.Client;
        using var createdResponse = await client.PostAsJsonAsync(Path, Body()); var created = await Json(createdResponse, HttpStatusCode.Created); var id = created.GetProperty("id").GetGuid();
        Assert.Equal("UNFIT", created.GetProperty("fitness").GetString()); Assert.Equal(JsonValueKind.Null, created.GetProperty("nextEvaluationOn").ValueKind);
        using var edit = await client.PutAsJsonAsync($"{Path}/{id}", Body(revision: 1, fitness: "FIT")); await Json(edit, HttpStatusCode.OK);
        using var stale = await client.PutAsJsonAsync($"{Path}/{id}", Body(revision: 1)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var revoke = await client.PostAsJsonAsync($"{Path}/{id}/revoke", new { unitId = database.UnitA, expectedRevision = 2, reasonCode = "DUPLICATE", confirmation = true }); var revoked = await Json(revoke, HttpStatusCode.OK);
        using var reread = await client.GetAsync($"{Path}/{id}?unitId={database.UnitA}"); Assert.Equal(revoked.ToString(), (await Json(reread, HttpStatusCode.OK)).ToString());
        using var historyResponse = await client.GetAsync($"{Path}/{id}/history?unitId={database.UnitA}"); var history = await Json(historyResponse, HttpStatusCode.OK);
        Assert.Equal(3, history.GetArrayLength()); Assert.Equal("DUPLICATE", history[0].GetProperty("reasonCode").GetString());
        Assert.DoesNotContain("physician", history.ToString(), StringComparison.OrdinalIgnoreCase);
        await using var db = database.CreateContext(); var stored = await db.OccupationalCertificates.SingleAsync(a => a.Id == id);
        Assert.Equal(3, stored.Revision); Assert.True(stored.Active); Assert.Null(stored.DeletedAt);
        Assert.DoesNotContain("987654", stored.ProtectedDetails); Assert.DoesNotContain("administrativa", stored.ProtectedDetails);
        foreach (var a in await db.AuditLogs.Where(a => a.ResourceId == id).ToArrayAsync()) { Assert.True(a.IsIntegrityValid()); Assert.DoesNotContain("Emitente restrito", a.NewData); Assert.DoesNotContain("987654", a.NewData); Assert.DoesNotContain("fitness", a.NewData!, StringComparison.OrdinalIgnoreCase); }
    }

    [PostgresFact]
    public async Task HumanResourcesGetsReducedDataAndWorkerOnlyOwnRecords()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var medical = doctor.Client;
        var worker = await Account(factory, "TRABALHADOR"); using var own = worker.Client; var hr = await Account(factory, "RH"); using var reader = hr.Client;
        using var created = await medical.PostAsJsonAsync(Path, Body(worker.Worker)); var mine = await Json(created, HttpStatusCode.Created); var id = mine.GetProperty("id").GetGuid();
        using var foreignResponse = await medical.PostAsJsonAsync(Path, Body()); var foreign = await Json(foreignResponse, HttpStatusCode.Created);
        using var hrRead = await reader.GetAsync($"{Path}/{id}?unitId={database.UnitA}"); var limited = await Json(hrRead, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, limited.GetProperty("medical").ValueKind); Assert.Equal("UNFIT", limited.GetProperty("fitness").GetString()); Assert.DoesNotContain("987654", limited.ToString());
        using var ownRead = await own.GetAsync($"{Path}/{id}?unitId={database.UnitA}"); var full = await Json(ownRead, HttpStatusCode.OK); Assert.Equal("987654", full.GetProperty("medical").GetProperty("physicianCrm").GetString());
        using var ownList = await own.GetAsync($"{Path}?unitId={database.UnitA}"); var list = await Json(ownList, HttpStatusCode.OK); Assert.Equal(1, list.GetProperty("total").GetInt32());
        using var notMine = await own.GetAsync($"{Path}/{foreign.GetProperty("id").GetGuid()}/history?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);
        foreach (var c in new[] { own, reader }) { using var denied = await c.PostAsJsonAsync(Path, Body(worker.Worker)); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
        using var outside = await medical.GetAsync($"{Path}?unitId={database.UnitB}"); Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
    }

    [PostgresFact]
    public async Task GenericAdministrativeAndAuditGrantsNeverUnlockTheHealthRegistry()
    {
        await using var factory = new PostgresApiFactory(database);
        foreach (var role in new[] { "ADMIN", "GESTOR", "TECNICO", "AUDITOR" }) {
            var account = await Account(factory, role); using var client = account.Client;
            using var response = await client.GetAsync($"{Path}/context?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [PostgresFact]
    public async Task LatestPerWorkerPreventsOldFitnessFromAppearingAsCurrent()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var client = doctor.Client;
        var subject = await Account(factory, "TRABALHADOR"); using var own = subject.Client;
        using var oldResponse = await client.PostAsJsonAsync(Path, Body(subject.Worker, exam: Today.AddDays(-5), fitness: "FIT")); var old = await Json(oldResponse, HttpStatusCode.Created);
        using var newResponse = await client.PostAsJsonAsync(Path, Body(subject.Worker, exam: Today, fitness: "UNFIT")); var newer = await Json(newResponse, HttpStatusCode.Created);
        using var latestResponse = await own.GetAsync($"{Path}?unitId={database.UnitA}"); var latest = await Json(latestResponse, HttpStatusCode.OK);
        Assert.Equal(1, latest.GetProperty("total").GetInt32()); Assert.Equal("UNFIT", latest.GetProperty("items")[0].GetProperty("fitness").GetString());
        using var oldRead = await own.GetAsync($"{Path}/{old.GetProperty("id").GetGuid()}?unitId={database.UnitA}"); Assert.False((await Json(oldRead, HttpStatusCode.OK)).GetProperty("isLatest").GetBoolean());
        using var allResponse = await own.GetAsync($"{Path}?unitId={database.UnitA}&view=ALL"); Assert.Equal(2, (await Json(allResponse, HttpStatusCode.OK)).GetProperty("total").GetInt32());
        using var revoke = await client.PostAsJsonAsync($"{Path}/{newer.GetProperty("id").GetGuid()}/revoke", new { unitId = database.UnitA, expectedRevision = 1, reasonCode = "ENTRY_ERROR", confirmation = true }); await Json(revoke, HttpStatusCode.OK);
        using var previous = await own.GetAsync($"{Path}?unitId={database.UnitA}"); Assert.Equal(old.GetProperty("id").GetGuid(), (await Json(previous, HttpStatusCode.OK)).GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [PostgresFact]
    public async Task InvalidDatesUnknownClinicalFieldsAndDuplicateCommandsAreRejected()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var client = doctor.Client; var command = Guid.NewGuid();
        using var first = await client.PostAsJsonAsync(Path, Body(command: command)); await Json(first, HttpStatusCode.Created);
        using var duplicate = await client.PostAsJsonAsync(Path, Body(command: command)); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        foreach (var body in new[] { Body(exam: Today.AddDays(1)), Body(fitness: ""), Body(next: Today.AddDays(-1)) }) { using var r = await client.PostAsJsonAsync(Path, body); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Body()))!; node["diagnosis"] = "Forbidden field";
        using var clinical = await client.PostAsJsonAsync(Path, node); Assert.Equal(HttpStatusCode.BadRequest, clinical.StatusCode);
        node.AsObject().Remove("diagnosis"); node["details"]!["clinicalNotes"] = "Forbidden nested field";
        using var nested = await client.PostAsJsonAsync(Path, node); Assert.Equal(HttpStatusCode.BadRequest, nested.StatusCode);
        using var foreign = await client.PostAsJsonAsync(Path, Body(database.WorkerB)); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [PostgresFact]
    public async Task CorruptProtectedDataFailsClosedWithoutInventingFitness()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var client = doctor.Client;
        var subject = await Account(factory, "TRABALHADOR"); using var own = subject.Client;
        using var create = await client.PostAsJsonAsync(Path, Body(subject.Worker)); var id = (await Json(create, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        await using var db = database.CreateContext(); await db.OccupationalCertificates.Where(a => a.Id == id).ExecuteUpdateAsync(set => set.SetProperty(a => a.ProtectedDetails, "corrupt-payload"));
        using var get = await own.GetAsync($"{Path}/{id}?unitId={database.UnitA}"); var result = await Json(get, HttpStatusCode.ServiceUnavailable);
        Assert.DoesNotContain("corrupt-payload", result.ToString()); Assert.DoesNotContain("fitness", result.ToString());
        using var list = await own.GetAsync($"{Path}?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        await db.OccupationalCertificates.Where(a => a.Id == id).ExecuteUpdateAsync(set => set.SetProperty(a => a.Active, false));
    }

    [PostgresFact]
    public async Task ConcurrentCorrectionsPreserveTheWinningRevisionAndOnlyOneAudit()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var client = doctor.Client;
        using var create = await client.PostAsJsonAsync(Path, Body()); var id = (await Json(create, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var results = await Task.WhenAll(client.PutAsJsonAsync($"{Path}/{id}", Body(revision: 1, fitness: "FIT")), client.PutAsJsonAsync($"{Path}/{id}", Body(revision: 1, fitness: "UNFIT")));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); } finally { foreach (var r in results) r.Dispose(); }
        await using var db = database.CreateContext(); Assert.Equal(2, (await db.OccupationalCertificates.SingleAsync(a => a.Id == id)).Revision);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.ResourceId == id && a.ResourceType == "aso_registro"));
    }

    [PostgresFact]
    public async Task DateFiltersApplyBeforePagingWithoutInventingAMissingNextEvaluation()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var client = doctor.Client;
        var subject = await Account(factory, "TRABALHADOR"); using var own = subject.Client;
        foreach (var next in new DateOnly?[] { Today.AddDays(-1), Today, Today.AddDays(30), Today.AddDays(31), null }) {
            using var r = await client.PostAsJsonAsync(Path, Body(subject.Worker, exam: Today.AddDays(-5), next: next)); await Json(r, HttpStatusCode.Created);
        }
        using var soon = await own.GetAsync($"{Path}?unitId={database.UnitA}&view=ALL&due=SOON&page=2&pageSize=1"); var p = await Json(soon, HttpStatusCode.OK);
        Assert.Equal(2, p.GetProperty("total").GetInt32()); Assert.Equal(1, p.GetProperty("items").GetArrayLength());
        using var noDate = await own.GetAsync($"{Path}?unitId={database.UnitA}&view=ALL&due=NO_DATE"); Assert.Equal(1, (await Json(noDate, HttpStatusCode.OK)).GetProperty("total").GetInt32());
    }

    [PostgresFact]
    public async Task CustomGrantUsesOnlyItsSectorAndIsImmediatelyRevokedFromTheDatabase()
    {
        await using var factory = new PostgresApiFactory(database); var doctor = await Account(factory, "MEDICO"); using var medical = doctor.Client;
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var sector = new Department(database.UnitA, "Setor ASO QA", null, null);
        var worker = new Worker(database.CompanyA, "SET-" + key[..16], [1, 2, 3], key + key, "Pessoa no setor QA", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, sector.Id, null);
        var user = new User("Leitor de setor", key + "@example.invalid", "fixture-only-hash"); var role = new Role("ASO_" + key[..20], "Consulta administrativa restrita", "Fixture de concessão explícita");
        var grant = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "aso" && p.Action == "acompanhar")).Id);
        var scope = new UserScope(user.Id, "SETOR"); scope.Update(user.Id, "SETOR", sector.Id);
        db.AddRange(sector, worker, user, role, grant, scope, new UserRole(user.Id, role.Id, database.CompanyA)); await db.SaveChangesAsync();
        using var created = await medical.PostAsJsonAsync(Path, Body(worker.Id)); var id = (await Json(created, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var outsideCreated = await medical.PostAsJsonAsync(Path, Body()); var outside = (await Json(outsideCreated, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString());
        using var list = await client.GetAsync($"{Path}?unitId={database.UnitA}"); var page = await Json(list, HttpStatusCode.OK);
        Assert.Equal(1, page.GetProperty("total").GetInt32()); Assert.Equal(id, page.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("items")[0].GetProperty("medical").ValueKind);
        using var deniedRow = await client.GetAsync($"{Path}/{outside}?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.NotFound, deniedRow.StatusCode);
        using var deniedWrite = await client.PostAsJsonAsync(Path, Body(worker.Id)); Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);
        var sectorMedical = await Account(factory, "MEDICO", "SETOR", sector.Id); using var restrictedDoctor = sectorMedical.Client;
        using var noWholeUnit = await restrictedDoctor.PostAsJsonAsync(Path, Body(worker.Id)); Assert.Equal(HttpStatusCode.Forbidden, noWholeUnit.StatusCode);
        await db.RolePermissions.Where(p => p.Id == grant.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.Active, false));
        using var revoked = await client.GetAsync($"{Path}/{id}?unitId={database.UnitA}"); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }
}
