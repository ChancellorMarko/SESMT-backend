using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DocumentGrantPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task MedicalEvidence_RequiresExplicitReadAndReviewGrantsAndHonorsRevocation()
    {
        var typeId = await SeedConferentAsync(SecrecyLevel.Medical);
        await using var factory = new PostgresApiFactory(database);
        using var actor = factory.CreateClient(); actor.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var outside = factory.CreateClient(); outside.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorB.ToString());
        var doc = await CreateAsync(actor, typeId, SecrecyLevel.Medical);
        var id = doc.GetProperty("id").GetGuid();
        await StatusAsync(await actor.GetAsync($"/documents/{id}"), HttpStatusCode.NotFound);
        var readGrant = await GrantAsync(actor, id, false, 1);
        var revision = await RevisionAsync(actor, id);
        var upload = await UploadAsync(actor, id, revision);
        revision = await RevisionAsync(actor, id);
        var versionId = upload.GetProperty("id").GetGuid();
        var fileId = upload.GetProperty("storedFileId").GetGuid();
        await StatusAsync(await outside.GetAsync($"/documents/{id}"), HttpStatusCode.NotFound);
        await StatusAsync(await outside.GetAsync($"/documents/{id}/files/{fileId}/download"), HttpStatusCode.NotFound);
        using (var download = await actor.GetAsync($"/documents/{id}/files/{fileId}/download"))
        {
            await StatusAsync(download, HttpStatusCode.OK, dispose: false);
            Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
            Assert.Equal("%PDF-1.7\nfictitious clinical evidence", await download.Content.ReadAsStringAsync());
        }
        await StatusAsync(await actor.PostAsJsonAsync($"/documents/{id}/versions/{versionId}/review",
            new { expectedVersion = revision, version = 1, decision = "aceita" }), HttpStatusCode.Forbidden);

        var reviewGrant = await GrantAsync(actor, id, true, revision);
        revision = await RevisionAsync(actor, id);
        using (var accepted = await actor.PostAsJsonAsync($"/documents/{id}/versions/{versionId}/review",
            new { expectedVersion = revision, version = 1, decision = "aceita" }))
        {
            await StatusAsync(accepted, HttpStatusCode.OK, dispose: false);
            var result = await PayloadAsync(accepted);
            Assert.Equal(database.ActorA, result.GetProperty("approverId").GetGuid());
            Assert.Equal(DocumentVersionState.Current, result.GetProperty("state").GetString());
        }
        using (var search = await actor.GetAsync($"/documents/query?documentTypeId={typeId}"))
        {
            await StatusAsync(search, HttpStatusCode.OK, dispose: false);
            Assert.Equal(0, (await PayloadAsync(search)).GetProperty("total").GetInt32());
        }

        await StatusAsync(await actor.PostAsync($"/documents/{id}/access-grants/{reviewGrant}/revoke?expectedVersion=1", null), HttpStatusCode.NoContent);
        revision = await RevisionAsync(actor, id);
        var next = await UploadAsync(actor, id, revision);
        revision = await RevisionAsync(actor, id);
        await StatusAsync(await actor.PostAsJsonAsync($"/documents/{id}/versions/{next.GetProperty("id").GetGuid()}/review",
            new { expectedVersion = revision, version = 2, decision = "aceita" }), HttpStatusCode.Forbidden);
        await StatusAsync(await actor.PostAsync($"/documents/{id}/access-grants/{readGrant}/revoke?expectedVersion=1", null), HttpStatusCode.NoContent);
        await StatusAsync(await actor.GetAsync($"/documents/{id}"), HttpStatusCode.NotFound);
        await StatusAsync(await actor.GetAsync($"/documents/{id}/files/{fileId}/download"), HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task RestrictedGrant_CannotExtendCompanyScopeAndExpiresImmediately()
    {
        var typeId = await SeedConferentAsync(SecrecyLevel.Restricted);
        await using var factory = new PostgresApiFactory(database);
        using var actor = factory.CreateClient(); actor.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var outside = factory.CreateClient(); outside.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorB.ToString());
        var doc = await CreateAsync(actor, typeId, SecrecyLevel.Restricted);
        var id = doc.GetProperty("id").GetGuid();
        await StatusAsync(await actor.GetAsync($"/documents/{id}"), HttpStatusCode.NotFound);
        await StatusAsync(await actor.PostAsJsonAsync($"/documents/{id}/access-grants", new
        {
            userId = database.ActorB, canReview = true, expiresAt = DateTimeOffset.UtcNow.AddHours(2), expectedVersion = 1,
        }), HttpStatusCode.Forbidden);
        var grantId = await GrantAsync(actor, id, false, 1);
        Assert.Equal(2, await RevisionAsync(actor, id));
        await StatusAsync(await outside.GetAsync($"/documents/{id}"), HttpStatusCode.NotFound);
        await using (var db = database.CreateContext())
        {
            // Simulate elapsed time in the isolated database without changing application clocks.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE docs.documento_concessao SET expira_em = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE id = {grantId}");
        }
        await StatusAsync(await actor.GetAsync($"/documents/{id}"), HttpStatusCode.NotFound);
    }

    private async Task<Guid> SeedConferentAsync(string secrecy)
    {
        await using var db = database.CreateContext();
        var role = new Role($"QAG_{Guid.NewGuid():N}", "Conferente fictício", "Somente no banco isolado de QA");
        db.Add(role); db.Add(new UserRole(database.ActorA, role.Id, database.CompanyA));
        foreach (var (resource, action) in new[] { ("documento", "aprovar"), ("aso", "ler"), ("aso", "editar") })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Resource == resource && p.Action == action);
            if (permission is null) { permission = new(resource, action, "Permissão fictícia de QA"); db.Add(permission); }
            db.Add(new RolePermission(role.Id, permission.Id));
        }
        db.Add(new OperationalDesignation(database.CompanyA, database.WorkerA,
            WorkPermitServiceCatalogEntry.All.Select(s => s.Id), [database.UnitA], "conferir", DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(3), "Designação fictícia de QA", database.ActorA));
        var type = new DocumentType($"QA-MED-{Guid.NewGuid():N}", "Evidência fictícia protegida", null, "Saúde", secrecy, false, true, null, null, "{}", 1);
        db.Add(type); await db.SaveChangesAsync();
        return type.Id;
    }

    private async Task<JsonElement> CreateAsync(HttpClient client, Guid typeId, string secrecy)
    {
        using var response = await client.PostAsJsonAsync("/documents", new
        {
            documentTypeId = typeId, companyId = database.CompanyA, unitId = database.UnitA, ownerId = database.ActorA,
            personId = database.WorkerA, title = "Exemplo fictício, sem conteúdo clínico real", code = $"QA-{Guid.NewGuid():N}",
            secrecyLevel = secrecy, issueDate = "2026-01-01", metadata = "{}",
        });
        await StatusAsync(response, HttpStatusCode.Created, dispose: false);
        return await PayloadAsync(response);
    }

    private async Task<Guid> GrantAsync(HttpClient client, Guid id, bool review, int revision)
    {
        using var response = await client.PostAsJsonAsync($"/documents/{id}/access-grants", new
        {
            userId = database.ActorA, canReview = review, expiresAt = DateTimeOffset.UtcNow.AddHours(2), expectedVersion = revision,
        });
        await StatusAsync(response, HttpStatusCode.Created, dispose: false);
        return (await PayloadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<int> RevisionAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/documents/{id}");
        await StatusAsync(response, HttpStatusCode.OK, dispose: false);
        return (await PayloadAsync(response)).GetProperty("revision").GetInt32();
    }

    private static async Task<JsonElement> UploadAsync(HttpClient client, Guid id, int revision)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(revision.ToString()), "expectedVersion");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\nfictitious clinical evidence"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "evidence.pdf");
        using var response = await client.PostAsync($"/documents/{id}/files", form);
        await StatusAsync(response, HttpStatusCode.Created, dispose: false);
        return await PayloadAsync(response);
    }

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task StatusAsync(HttpResponseMessage response, HttpStatusCode expected, bool dispose = true)
    {
        try { Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
        finally { if (dispose) response.Dispose(); }
    }
}
