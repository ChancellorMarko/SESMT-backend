using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Documents.Constants;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DocumentPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task Documents_ShouldHideAnotherCompanysDetailAndList()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        using var own = await client.GetAsync($"/documents/{database.DocumentA}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var outside = await client.GetAsync($"/documents/{database.DocumentB}");
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        using var outsideList = await client.GetAsync($"/documents?companyId={database.CompanyB}");
        Assert.Equal(HttpStatusCode.OK, outsideList.StatusCode);
        using var legacy = JsonDocument.Parse(await outsideList.Content.ReadAsStringAsync());
        Assert.Empty(legacy.RootElement.EnumerateArray());
        Assert.Equal("0", Assert.Single(outsideList.Headers.GetValues("X-Total-Count")));
        using var paged = await client.GetAsync($"/documents/query?companyId={database.CompanyB}");
        Assert.Equal(HttpStatusCode.OK, paged.StatusCode);
        using var page = JsonDocument.Parse(await paged.Content.ReadAsStringAsync());
        Assert.Empty(page.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.RootElement.GetProperty("total").GetInt32());
    }

    [PostgresFact]
    public async Task Upload_ShouldRemainPendingPreserveActorAndProtectTheStoredBytes()
    {
        await using var factory = new PostgresApiFactory(database);
        using var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorA.ToString());
        await using var before = database.CreateContext();
        var revision = (await before.Documents.SingleAsync(item => item.Id == database.DocumentA)).Revision;
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type /Catalog>>endobj\n%%EOF\n");
        MultipartFormDataContent FileBody(int expectedVersion)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedVersion");
            form.Add(new StringContent(database.ActorB.ToString()), "authorId");
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "fictitious-evidence.pdf");
            return form;
        }
        using var body = FileBody(revision);
        using var upload = await owner.PostAsync($"/documents/{database.DocumentA}/files", body);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        await using var stored = database.CreateContext();
        var version = await stored.DocumentVersions.SingleAsync(item => item.DocumentId == database.DocumentA);
        Assert.Equal(database.ActorA, version.AuthorId);
        Assert.Equal(DocumentVersionState.InReview, version.State);
        Assert.False(version.IsCurrent);
        Assert.Null(version.ApprovedAt);
        var fileCount = await stored.StoredFiles.CountAsync();
        using var staleBody = FileBody(revision);
        using var stale = await owner.PostAsync($"/documents/{database.DocumentA}/files", staleBody);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using (var verify = database.CreateContext())
        {
            Assert.Equal(fileCount, await verify.StoredFiles.CountAsync());
            Assert.Single(await verify.DocumentVersions.Where(item => item.DocumentId == database.DocumentA).ToArrayAsync());
        }
        using var downloaded = await owner.GetAsync($"/documents/{database.DocumentA}/files/{version.StoredFileId}/download");
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal(bytes, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("no-store", downloaded.Headers.CacheControl?.ToString());
        using var outsider = factory.CreateClient();
        outsider.DefaultRequestHeaders.Add("X-Test-Actor", database.ActorB.ToString());
        using var denied = await outsider.GetAsync($"/documents/{database.DocumentA}/files/{version.StoredFileId}/download");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
