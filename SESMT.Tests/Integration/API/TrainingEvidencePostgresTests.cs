using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class TrainingEvidencePostgresTests(PostgresDatabaseFixture database)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    private async Task<HttpClient> Admin(PostgresApiFactory factory)
    {
        await using var db = database.CreateContext(); var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (await db.Users.SingleAsync(u => u.Email.Value == "seed-admin@example.invalid")).Id.ToString()); return client;
    }
    private async Task<TrainingRecord> Training(Guid? worker = null)
    {
        await using var db = database.CreateContext(); var value = new TrainingRecord(database.CompanyA, database.UnitA, worker ?? database.WorkerA, Guid.NewGuid(), database.ActorA);
        value.Set("Curso interno QA", null, "INITIAL", Today.AddDays(-7), null, 90, null, null, null, null, null); db.Add(value); await db.SaveChangesAsync(); return value;
    }
    private async Task<(Guid Doc, Guid Version, int Revision)> Document(Guid? worker = null, bool published = true, bool signature = false, string secrecy = SecrecyLevel.Internal)
    {
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var type = new DocumentType("TE-" + key, "Comprovante QA", null, "QA", secrecy, signature, true, null, null, "{}", 1);
        var doc = new Document(type.Id, database.CompanyA, database.UnitA, null, "TE-" + key, "Registro documental QA", null, secrecy, Today, null, null, database.ActorA);
        var file = new StoredFile("local", "QA", key, "qa.pdf", "application/pdf", 20, new string('a', 64), false, "PENDENTE");
        var version = new DocumentVersion(doc.Id, 1, file.Id, file.FileHash, database.ActorA);
        version.SubmitForReview(); doc.RegisterVersion(1, database.ActorA);
        if (published) { version.Approve(database.ActorA); version.Publish(); doc.RecordReview(true, database.ActorA); }
        db.AddRange(type, doc, file, version, new DocumentLink(doc.Id, LinkedEntityType.Worker, worker ?? database.WorkerA));
        await db.SaveChangesAsync(); return (doc.Id, version.Id, doc.Revision);
    }
    private string Url(TrainingRecord training) => $"/api/v1/trainings/{training.Id}/evidence";
    private async Task<HttpResponseMessage> Bind(HttpClient client, TrainingRecord training, (Guid Doc, Guid Version, int Revision) doc, int? revision = null) =>
        await client.PostAsJsonAsync(Url(training), new { unitId = training.UnitId, expectedRevision = revision ?? training.Revision,
            documentId = doc.Doc, versionId = doc.Version, expectedDocumentRevision = doc.Revision, reason = "Documento conferido para este treinamento", confirmation = true });
    private async Task<JsonElement> Payload(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [PostgresFact]
    public async Task PinsExactVersionAndUnlinkPreservesDocumentAndAudit()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var training = await Training(); var doc = await Document();
        using var linked = await Bind(client, training, doc); Assert.Equal(HttpStatusCode.OK, linked.StatusCode); var body = await Payload(linked);
        Assert.Equal("CURRENT", body.GetProperty("state").GetString()); Assert.Equal(2, body.GetProperty("trainingRevision").GetInt32());
        Assert.Equal(doc.Version, body.GetProperty("evidence").GetProperty("versionId").GetGuid()); Assert.Equal(new string('a', 64), body.GetProperty("evidence").GetProperty("fileHash").GetString());
        using var removed = await client.PostAsJsonAsync(Url(training) + "/unlink", new { unitId = training.UnitId, expectedRevision = 2, reason = "Correção do vínculo documental", confirmation = true }); Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal("EMPTY", (await Payload(removed)).GetProperty("state").GetString());
        await using var db = database.CreateContext(); Assert.True((await db.Documents.SingleAsync(d => d.Id == doc.Doc)).Active);
        Assert.Null((await db.TrainingRecords.SingleAsync(t => t.Id == training.Id)).EvidenceDocumentId);
        var events = await db.AuditLogs.Where(a => a.ResourceId == training.Id && a.Action.StartsWith("TRAINING_EVIDENCE_")).OrderBy(a => a.Id).ToArrayAsync();
        Assert.Equal(2, events.Length); Assert.All(events, a => Assert.True(a.IsIntegrityValid())); Assert.Contains(doc.Version.ToString(), events[1].PreviousData!);
        Assert.False(await db.DocumentLinks.AnyAsync(l => l.DocumentId == doc.Doc && l.EntityType == LinkedEntityType.WorkPermit));
    }

    [PostgresFact]
    public async Task OtherPeopleDraftsMedicalAndUnsignedLegacyFilesAreNotOptions()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var training = await Training();
        var blocked = new[] { await Document(database.WorkerB), await Document(published: false), await Document(signature: true), await Document(secrecy: SecrecyLevel.Medical) };
        foreach (var doc in blocked) {
            using var response = await Bind(client, training, doc); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        await using var db = database.CreateContext();
        foreach (var doc in blocked) {
            var code = (await db.Documents.SingleAsync(d => d.Id == doc.Doc)).Code;
            using var options = await client.GetAsync(Url(training) + $"/options?unitId={training.UnitId}&q={code}"); Assert.Equal(HttpStatusCode.OK, options.StatusCode);
            Assert.Equal(0, (await Payload(options)).GetProperty("total").GetInt32());
        }
        Assert.Null((await db.TrainingRecords.SingleAsync(t => t.Id == training.Id)).EvidenceDocumentId);
    }

    [PostgresFact]
    public async Task ChangesRequireNewConfirmationAndNeverSilentlyAdoptAnotherVersion()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var training = await Training(); var doc = await Document();
        using var linked = await Bind(client, training, doc); Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        await using (var db = database.CreateContext()) { var item = await db.TrainingRecords.SingleAsync(t => t.Id == training.Id); item.Set("Curso corrigido", null, "INITIAL", Today.AddDays(-7), null, 120, null, null, null, null, null); item.RecordUpdate(database.ActorA); await db.SaveChangesAsync(); }
        using var changed = await client.GetAsync(Url(training) + $"?unitId={training.UnitId}"); Assert.Equal("TRAINING_CHANGED", (await Payload(changed)).GetProperty("state").GetString());
        using var reconfirmed = await Bind(client, training, doc, 3); Assert.Equal(HttpStatusCode.OK, reconfirmed.StatusCode);
        Guid newVersion;
        await using (var db = database.CreateContext()) {
            var document = await db.Documents.SingleAsync(d => d.Id == doc.Doc); var old = await db.DocumentVersions.SingleAsync(v => v.Id == doc.Version); old.Supersede(); await db.SaveChangesAsync();
            var file = new StoredFile("local", "QA", Guid.NewGuid().ToString(), "other.pdf", "application/pdf", 20, new string('b', 64), false, "PENDENTE");
            var version = new DocumentVersion(doc.Doc, 2, file.Id, file.FileHash, database.ActorA); version.SubmitForReview(); version.Approve(database.ActorA); version.Publish();
            document.RegisterVersion(2, database.ActorA); document.RecordReview(true, database.ActorA); db.AddRange(file, version); await db.SaveChangesAsync(); newVersion = version.Id;
        }
        using var replacement = await client.GetAsync(Url(training) + $"?unitId={training.UnitId}"); var receipt = await Payload(replacement);
        Assert.Equal("DOCUMENT_CHANGED", receipt.GetProperty("state").GetString()); Assert.Equal(doc.Version, receipt.GetProperty("evidence").GetProperty("versionId").GetGuid());
        Assert.NotEqual(newVersion, receipt.GetProperty("evidence").GetProperty("versionId").GetGuid());
    }

    [PostgresFact]
    public async Task StaleVersionsAndTwoConcurrentBindingsDoNotOverwriteOneAnother()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory); var training = await Training(); var doc = await Document();
        using var staleDoc = await Bind(client, training, (doc.Doc, doc.Version, doc.Revision + 1)); Assert.Equal(HttpStatusCode.Conflict, staleDoc.StatusCode);
        var replies = await Task.WhenAll(Bind(client, training, doc), Bind(client, training, doc));
        try { Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var reply in replies) reply.Dispose(); }
        await using var db = database.CreateContext(); Assert.Equal(2, (await db.TrainingRecords.SingleAsync(t => t.Id == training.Id)).Revision);
    }

    [PostgresFact]
    public async Task LostDocumentAccessHidesAllFileIdentifiersAndMetadata()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory); var training = await Training(); var doc = await Document(); using var linked = await Bind(admin, training, doc); Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        await using var db = database.CreateContext(); var key = Guid.NewGuid().ToString("N"); var user = new User("Consulta de treinamento", key + "@example.invalid", "fixture-hash"); var role = new Role("TE_" + key, "Consulta", "Somente treinamento");
        var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
        db.AddRange(user, role, scope, new UserRole(user.Id, role.Id, database.CompanyA), new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Resource == "treinamento" && p.Action == "ler")).Id)); await db.SaveChangesAsync();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", user.Id.ToString());
        using var response = await client.GetAsync(Url(training) + $"?unitId={training.UnitId}"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var text = await response.Content.ReadAsStringAsync(); var body = await Payload(response);
        Assert.Equal("UNAVAILABLE", body.GetProperty("state").GetString()); Assert.Equal(JsonValueKind.Null, body.GetProperty("evidence").ValueKind);
        Assert.DoesNotContain(doc.Doc.ToString(), text); Assert.DoesNotContain(doc.Version.ToString(), text); Assert.DoesNotContain("Registro documental QA", text);
    }

    [PostgresFact]
    public async Task WorkerReadsOwnBindingWithoutManagementAndCannotReadOtherTrainings()
    {
        await using var factory = new PostgresApiFactory(database); using var admin = await Admin(factory); Guid workerId, userId;
        await using (var db = database.CreateContext()) {
            var key = Guid.NewGuid().ToString("N"); var user = new User("Pessoa com comprovante", key + "@example.invalid", "fixture-hash");
            var worker = new Worker(database.CompanyA, "TE-" + key[..20], [1, 2, 3], key + key, "Pessoa com comprovante", new(1990, 1, 1), new(2020, 1, 1), null, null, database.UnitA, null, user.Id);
            var scope = new UserScope(user.Id, "EMPRESA"); scope.Update(user.Id, "EMPRESA", database.CompanyA);
            db.AddRange(user, worker, scope, new UserRole(user.Id, (await db.Roles.SingleAsync(r => r.Code == "TRABALHADOR")).Id, database.CompanyA)); await db.SaveChangesAsync(); workerId = worker.Id; userId = user.Id;
        }
        var training = await Training(workerId); var doc = await Document(workerId); using var linked = await Bind(admin, training, doc); Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Actor", userId.ToString());
        using var own = await client.GetAsync(Url(training) + $"?unitId={training.UnitId}"); Assert.Equal(HttpStatusCode.OK, own.StatusCode); Assert.False((await Payload(own)).GetProperty("canManage").GetBoolean());
        using var options = await client.GetAsync(Url(training) + $"/options?unitId={training.UnitId}"); Assert.Equal(HttpStatusCode.Forbidden, options.StatusCode);
        var other = await Training(); using var foreign = await client.GetAsync(Url(other) + $"?unitId={training.UnitId}"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var denied = await Bind(client, training, doc, 2); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [PostgresFact]
    public async Task ChangedFileOrDocumentPoliciesAreReevaluated()
    {
        await using var factory = new PostgresApiFactory(database); using var client = await Admin(factory);
        foreach (var (change, expected) in new[] { ("signature", "SIGNATURE_PENDING"), ("classification", "UNAVAILABLE"), ("scan", "UNAVAILABLE"), ("expiry", "EXPIRED") }) {
        var training = await Training(); var doc = await Document();
        using var linked = await Bind(client, training, doc); Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        await using (var db = database.CreateContext()) {
            var document = await db.Documents.SingleAsync(d => d.Id == doc.Doc); var type = await db.DocumentTypes.SingleAsync(t => t.Id == document.DocumentTypeId);
            if (change == "signature") db.Entry(type).Property(t => t.RequiresSignature).CurrentValue = true;
            if (change == "classification") db.Entry(type).Property(t => t.SecrecyLevel).CurrentValue = SecrecyLevel.Medical;
            if (change == "expiry") { db.Entry(document).Property(d => d.IssueDate).CurrentValue = Today.AddDays(-10); db.Entry(document).Property(d => d.ValidUntilDate).CurrentValue = Today.AddDays(-1); }
            if (change == "scan") { var version = await db.DocumentVersions.SingleAsync(v => v.Id == doc.Version); var file = await db.StoredFiles.SingleAsync(f => f.Id == version.StoredFileId); db.Entry(file).Property(f => f.AntivirusScanResult).CurrentValue = "INFECTED"; }
            await db.SaveChangesAsync();
        }
        using var response = await client.GetAsync(Url(training) + $"?unitId={training.UnitId}"); var receipt = await Payload(response);
        Assert.Equal(expected, receipt.GetProperty("state").GetString());
        if (expected == "UNAVAILABLE") Assert.Equal(JsonValueKind.Null, receipt.GetProperty("evidence").ValueKind);
        }
    }
}
