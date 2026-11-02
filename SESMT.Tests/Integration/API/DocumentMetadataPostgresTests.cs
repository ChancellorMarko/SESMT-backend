using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DocumentMetadataPostgresTests(PostgresDatabaseFixture database)
{
    private HttpClient Client(PostgresApiFactory factory, Guid? actor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (actor ?? database.ActorA).ToString());
        return client;
    }
    private static object Body(int expected, string title) => new { expectedVersion = expected, title,
        description = "Descrição fictícia", secrecyLevel = "Interno", issueDate = "2026-01-01",
        validUntilDate = "2027-01-01", keywords = new[] { "QA" }, metadata = "{\"hours\":8}" };

    [PostgresFact]
    public async Task Update_RequiresRevisionAndConfirmsPersistentFieldsWithoutCreatingFile()
    {
        await using var factory = new PostgresApiFactory(database);
        var id = await TestDocumentSetup.CreateAsync(database);
        using var client = Client(factory);
        using var missing = await client.PatchAsJsonAsync($"/api/v1/documents/{id}", Body(0, "Inválido"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var saved = await client.PatchAsJsonAsync($"/api/v1/documents/{id}", Body(1, "Corrigido"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var receipt = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, receipt.GetProperty("revision").GetInt32());
        Assert.Equal(0, receipt.GetProperty("currentVersionNumber").GetInt32());
        using var stale = await client.PatchAsJsonAsync($"/api/v1/documents/{id}", Body(1, "Não sobrescrever"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = database.CreateContext();
        var document = await db.Documents.SingleAsync(d => d.Id == id);
        Assert.Equal("Corrigido", document.Title); Assert.Equal(2, document.Revision);
        using var metadata = JsonDocument.Parse(document.Metadata);
        Assert.Equal(8, metadata.RootElement.GetProperty("hours").GetInt32()); Assert.Equal(database.ActorA, document.OwnerId);
        Assert.False(await db.DocumentVersions.AnyAsync(v => v.DocumentId == id));
    }

    [PostgresFact]
    public async Task TwoHttpEditors_OnlyOneCanCommitTheOriginalRevision()
    {
        await using var factory = new PostgresApiFactory(database);
        var id = await TestDocumentSetup.CreateAsync(database);
        using var client = Client(factory);
        var replies = await Task.WhenAll(client.PatchAsJsonAsync($"/api/v1/documents/{id}", Body(1, "Editor A")),
            client.PatchAsJsonAsync($"/api/v1/documents/{id}", Body(1, "Editor B")));
        try
        {
            Assert.Single(replies, reply => reply.StatusCode == HttpStatusCode.OK);
            Assert.Single(replies, reply => reply.StatusCode == HttpStatusCode.Conflict);
            var accepted = await replies.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<JsonElement>();
            await using var db = database.CreateContext();
            var stored = await db.Documents.SingleAsync(d => d.Id == id);
            Assert.Equal(accepted.GetProperty("title").GetString(), stored.Title); Assert.Equal(2, stored.Revision);
        }
        finally { foreach (var reply in replies) reply.Dispose(); }
    }

    [PostgresFact]
    public async Task DatabaseConcurrencyToken_RejectsTwoAlreadyLoadedEditorsAtomically()
    {
        var id = await TestDocumentSetup.CreateAsync(database);
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var a = await first.Documents.SingleAsync(d => d.Id == id); var b = await second.Documents.SingleAsync(d => d.Id == id);
        a.UpdateContent("Primeiro", null, [], "{\"writer\":1}"); a.TouchRevision(database.ActorA);
        b.UpdateContent("Segundo", null, [], "{\"writer\":2}"); b.TouchRevision(database.ActorA);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verify = database.CreateContext();
        var saved = await verify.Documents.SingleAsync(d => d.Id == id);
        using var metadata = JsonDocument.Parse(saved.Metadata);
        Assert.Equal("Primeiro", saved.Title); Assert.Equal(1, metadata.RootElement.GetProperty("writer").GetInt32()); Assert.Equal(2, saved.Revision);
    }

    [PostgresFact]
    public async Task ForeignCompanyCannotEditEvenWithTheCurrentRevision()
    {
        await using var factory = new PostgresApiFactory(database);
        var id = await TestDocumentSetup.CreateAsync(database);
        using var client = Client(factory, database.ActorB);
        using var denied = await client.PatchAsJsonAsync($"/api/v1/documents/{id}", Body(1, "Fora do escopo"));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await using var db = database.CreateContext(); Assert.Equal(1, (await db.Documents.SingleAsync(d => d.Id == id)).Revision);
    }
}
