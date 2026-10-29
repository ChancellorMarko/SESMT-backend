using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SESMT.Domain.Modules.Documents.Constants;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class DocumentServicePostgresTests(PostgresDatabaseFixture database)
{
    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    private HttpClient AuthClient(PostgresApiFactory factory, Guid? actor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Actor", (actor ?? database.ActorA).ToString());
        return client;
    }

    private static byte[] PdfBytes() =>
        Encoding.ASCII.GetBytes("%PDF-1.7\nfictitious evidence\n%%EOF");

    private static MultipartFormDataContent FileForm(int expectedVersion, string fileName = "evidence.pdf")
    {
        var form = new MultipartFormDataContent();
        form.Add(
            new StringContent(expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "expectedVersion");

        var file = new ByteArrayContent(PdfBytes());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);

        return form;
    }

    private static async Task<JsonElement> PayloadAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task AssertNot5xx(HttpResponseMessage response, string endpoint)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            (int)response.StatusCode < 500,
            $"Endpoint {endpoint} retornou {(int)response.StatusCode}: {body}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Autenticação / isolamento
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Documents_AnonymousRequest_ReturnsUnauthorized()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = factory.CreateClient(); // sem X-Test-Actor

        using var response = await client.GetAsync($"/documents?companyId={database.CompanyA}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgresFact]
    public async Task Documents_ListWithoutCompanyId_ReturnsBadRequest()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory);

        using var response = await client.GetAsync("/documents");

        await AssertNot5xx(response, "GET /documents");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await PayloadAsync(response);
        Assert.Equal("MISSING_PARAM", payload.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task Documents_ListOfAnotherCompany_ReturnsEmpty()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // ActorA não pertence à CompanyB → retorna lista vazia, não 404
        using var response = await client.GetAsync($"/documents?companyId={database.CompanyB}");

        await AssertNot5xx(response, "GET /documents?companyId=CompanyB");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(doc.RootElement.EnumerateArray());

        // Header X-Total-Count deve indicar 0
        Assert.Equal("0", Assert.Single(response.Headers.GetValues("X-Total-Count")));
    }

    [PostgresFact]
    public async Task Documents_GetByIdOfAnotherCompany_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // DocumentB pertence à CompanyB → ActorA recebe 404
        using var response = await client.GetAsync($"/documents/{database.DocumentB}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Upload
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Upload_CreatesVersionAndReturns201()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var form = FileForm(expectedVersion: 1);
        using var response = await client.PostAsync($"/documents/{database.DocumentA}/files", form);

        await AssertNot5xx(response, "POST /documents/{id}/files");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var version = await PayloadAsync(response);
        Assert.Equal(DocumentVersionState.InReview, version.GetProperty("state").GetString());
        Assert.False(version.GetProperty("isCurrent").GetBoolean());
    }

    [PostgresFact]
    public async Task Upload_WithStaleVersion_ReturnsConflict()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // Primeira versão (Revision=1 → 2)
        using (var first = FileForm(expectedVersion: 1))
        using (var okResponse = await client.PostAsync($"/documents/{database.DocumentA}/files", first))
        {
            Assert.Equal(HttpStatusCode.Created, okResponse.StatusCode);
        }

        // Segunda tentativa com expectedVersion=1 (mas Revision agora é 2) → 409
        using var stale = FileForm(expectedVersion: 1);
        using var response = await client.PostAsync($"/documents/{database.DocumentA}/files", stale);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [PostgresFact]
    public async Task Upload_WithInvalidContentType_ReturnsBadRequest()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "expectedVersion");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("not a pdf"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "not-a-pdf.txt");

        using var response = await client.PostAsync($"/documents/{database.DocumentA}/files", form);

        await AssertNot5xx(response, "POST /documents/{id}/files (invalid type)");
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            $"Esperado 400/422, obtido {(int)response.StatusCode}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Download
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Download_AfterUpload_ReturnsBytesWithNoStoreCache()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        // 1. Upload para criar versão + arquivo
        Guid storedFileId;
        using (var form = FileForm(expectedVersion: 1))
        using (var upload = await client.PostAsync($"/documents/{database.DocumentA}/files", form))
        {
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            var version = await PayloadAsync(upload);
            storedFileId = version.GetProperty("storedFileId").GetGuid();
        }

        // 2. Download
        using var response = await client.GetAsync(
            $"/documents/{database.DocumentA}/files/{storedFileId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(PdfBytes(), await response.Content.ReadAsByteArrayAsync());
    }

    [PostgresFact]
    public async Task Download_ByAnotherCompany_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var owner = AuthClient(factory, database.ActorA);
        using var outsider = AuthClient(factory, database.ActorB);

        // 1. Owner sobe a versão
        Guid storedFileId;
        using (var form = FileForm(expectedVersion: 1))
        using (var upload = await owner.PostAsync($"/documents/{database.DocumentA}/files", form))
        {
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            var version = await PayloadAsync(upload);
            storedFileId = version.GetProperty("storedFileId").GetGuid();
        }

        // 2. Outsider tenta baixar → 404
        using var response = await outsider.GetAsync(
            $"/documents/{database.DocumentA}/files/{storedFileId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Search (endpoint /documents/query)
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Query_SearchByTerm_ReturnsOnlyAccessibleDocuments()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.GetAsync("/documents/query?search=QA-DOC&page=1&pageSize=10");

        await AssertNot5xx(response, "GET /documents/query");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        // Não deve conter documentos de CompanyB
        Assert.All(items.EnumerateArray(), item =>
            Assert.Equal(database.CompanyA, item.GetProperty("companyId").GetGuid()));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Acesso a documento inexistente
    // ════════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Document_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new PostgresApiFactory(database);
        using var client = AuthClient(factory, database.ActorA);

        using var response = await client.GetAsync($"/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
