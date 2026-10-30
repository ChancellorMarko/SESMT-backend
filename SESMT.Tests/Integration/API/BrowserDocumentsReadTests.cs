using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SESMT.API.Controllers;
using SESMT.API.Services;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Tests.Integration.API;

// HTTP routing, signed JWT and native EF entities; InMemory is not PostgreSQL/storage homologation.
public sealed class BrowserDocumentsReadTests
{
    [Fact]
    public async Task BrowserProjection_IntersectsNativePolicyBeforeCountingAndReadingHistoryOrVersions()
    {
        await using var host = await Host.StartAsync();
        var allowed = host.AddDocument("VISIBLE");
        var hidden = host.AddDocument("NATIVE-POLICY-DENIED");
        var version = host.AddVersion(hidden);
        host.Policy.DeniedDocument = hidden.Id;
        await host.SaveAsync();

        var page = await host.JsonAsync("/api/v1/browser/documents");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(allowed.Id.ToString(), Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        foreach (var path in new[] { $"{hidden.Id}", $"{hidden.Id}/history", $"{hidden.Id}/versions", $"{hidden.Id}/versions/{version.Id}" })
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{path}")).StatusCode);
    }

    [Fact]
    public async Task Endpoints_RequireSignedBearerAndCurrentReadPermission()
    {
        await using var host = await Host.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/browser/documents")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "forged");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/v1/browser/document-types")).StatusCode);
        host.SignIn();
        host.Access.Value = host.Access.Value with { Grants = [] };
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/v1/browser/document-types")).StatusCode);
    }

    [Fact]
    public async Task List_FiltersCompanyUnitAndSectorBeforeCountingAndPaging()
    {
        await using var host = await Host.StartAsync();
        var allowed = host.AddDocument("B-02");
        host.AddDocument("A-01");
        host.AddDocument("C-03");
        var hidden = host.AddDocument("SECRET-UNIT", unit: host.OtherUnitId);
        host.AddDocument("SECRET-COMPANY", company: host.OtherCompanyId, unit: host.OtherUnitId);
        host.AddDocument("SECRET-SECTOR", sector: host.OtherSectorId);
        await host.SaveAsync();
        host.Access.Value = host.Access.Value with { FullUnitIds = [], SectorIds = [host.SectorId] };

        var page = await host.JsonAsync($"/api/v1/browser/documents?unitId={host.UnitId}&page=2&pageSize=1");
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        var doc = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(allowed.Id.ToString(), doc.GetProperty("id").GetString());
        Assert.True(doc.GetProperty("sourceReadOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.GetProperty("revision").ValueKind);
        Assert.Empty(doc.GetProperty("availableActions").EnumerateArray());
        Assert.Empty(doc.GetProperty("responsibleIds").EnumerateArray());
        Assert.Equal(host.UserId.ToString(), doc.GetProperty("ownerId").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{hidden.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/api/v1/browser/documents?unitId={host.OtherUnitId}")).StatusCode);
    }

    [Fact]
    public async Task List_AppliesSearchStatusTypeWorkerAndDateFilters()
    {
        await using var host = await Host.StartAsync();
        var worker = host.AddWorker();
        var match = host.AddDocument("NR-001", title: "Inspeção extraordinária", validUntil: new(2027, 1, 15));
        host.Db.DocumentLinks.Add(new(match.Id, LinkedEntityType.Worker, worker.Id));
        host.AddDocument("OTHER", title: "Treinamento", validUntil: new(2027, 3, 1));
        await host.SaveAsync();
        var page = await host.JsonAsync($"/api/v1/browser/documents?q=INSPE%C3%87%C3%83O&status=DRAFT&typeId={host.Type.Id}&workerId={worker.Id}&issueDateFrom=2026-10-01&issueDateTo=2026-10-31&validUntilFrom=2027-01-01&validUntilTo=2027-01-31");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        var doc = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(match.Id.ToString(), doc.GetProperty("id").GetString());
        Assert.Equal("2027-01-15", doc.GetProperty("validUntil").GetString());
        Assert.Equal(worker.Id.ToString(), Assert.Single(doc.GetProperty("workerIds").EnumerateArray()).GetString());
        Assert.Equal(0, (await host.JsonAsync("/api/v1/browser/documents?status=ACTIVE")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task UnitFilter_DoesNotIncludeCompanyWideDocumentsFromAnotherAuthorizedCompany()
    {
        await using var host = await Host.StartAsync();
        host.AddDocument("LOCAL", companyWide: true);
        host.AddDocument("OTHER-COMPANY", company: host.OtherCompanyId, companyWide: true);
        await host.SaveAsync();
        host.Access.Value = host.Access.Value with
        {
            CompanyIds = [host.CompanyId, host.OtherCompanyId], FullCompanyIds = [host.CompanyId, host.OtherCompanyId],
            Grants = [new("documents.read", host.CompanyId), new("documents.read", host.OtherCompanyId)]
        };
        var page = await host.JsonAsync($"/api/v1/browser/documents?unitId={host.UnitId}");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("LOCAL", Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("code").GetString());
        host.Access.Value = host.Access.Value with { Grants = [new("documents.read", host.OtherCompanyId)] };
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/api/v1/browser/documents?unitId={host.UnitId}")).StatusCode);
    }

    [Fact]
    public async Task WorkerFilter_RejectsWorkersOutsideLocationOrOwnLink()
    {
        await using var host = await Host.StartAsync();
        var worker = host.AddWorker();
        var otherWorker = host.AddWorker();
        var doc = host.AddDocument("LINKED");
        host.Db.DocumentLinks.Add(new(doc.Id, LinkedEntityType.Worker, otherWorker.Id));
        await host.SaveAsync();
        host.Access.Value = host.Access.Value with { OwnOnly = true, WorkerId = worker.Id };
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/api/v1/browser/documents?workerId={otherWorker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync($"/api/v1/browser/documents?workerId={Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("status=UNKNOWN")]
    [InlineData("unitId=00000000-0000-0000-0000-000000000000")]
    [InlineData("unitId=invalid")]
    [InlineData("validUntilFrom=2027-02-01&validUntilTo=2027-01-01")]
    [InlineData("issueDateFrom=2026-12-01&issueDateTo=2026-01-01")]
    public async Task InvalidFilters_ReturnBadRequest(string query)
    {
        await using var host = await Host.StartAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync($"/api/v1/browser/documents?{query}")).StatusCode);
    }

    [Fact]
    public async Task UnsupportedResponsibleFilter_IsExplicitInsteadOfMislabelingOwner()
    {
        await using var host = await Host.StartAsync();
        var response = await host.Client.GetAsync($"/api/v1/browser/documents?responsibleId={host.UserId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("UNSUPPORTED_FILTER", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompanyWideDocuments_RequireFullCompanyScope()
    {
        await using var host = await Host.StartAsync();
        var companyWide = host.AddDocument("COMPANY", companyWide: true);
        await host.SaveAsync();
        Assert.Equal(0, (await host.JsonAsync("/api/v1/browser/documents")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{companyWide.Id}")).StatusCode);
        host.Access.Value = host.Access.Value with { FullCompanyIds = [host.CompanyId] };
        var document = await host.JsonAsync($"/api/v1/browser/documents/{companyWide.Id}");
        Assert.Equal(JsonValueKind.Null, document.GetProperty("unitId").ValueKind);
        Assert.Equal(1, (await host.JsonAsync($"/api/v1/browser/documents?unitId={host.UnitId}")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task MedicalRestrictedAndMislabeledTypes_AreNotDisclosed()
    {
        await using var host = await Host.StartAsync();
        var medical = host.AddDocument("MEDICAL", secrecy: SecrecyLevel.Medical);
        var restricted = host.AddDocument("RESTRICTED", secrecy: SecrecyLevel.Restricted);
        var clinicalType = new DocumentType("ASO", "Clínico", null, "SST", SecrecyLevel.Medical, false, false, null, null, "{}", 1);
        host.Db.DocumentTypes.Add(clinicalType);
        var mislabeled = host.AddDocument("MISMATCH", type: clinicalType);
        host.AddDocument("VISIBLE");
        await host.SaveAsync();
        Assert.Equal(1, (await host.JsonAsync("/api/v1/browser/documents")).GetProperty("total").GetInt32());
        foreach (var id in new[] { medical.Id, restricted.Id, mislabeled.Id })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{id}/versions")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{id}/history")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/document-types/{clinicalType.Id}")).StatusCode);
    }

    [Fact]
    public async Task OwnOnly_RequiresLiveWorkerLinkAndHidesOtherWorkerIdentifiers()
    {
        await using var host = await Host.StartAsync();
        var own = host.AddWorker();
        var other = host.AddWorker();
        var visible = host.AddDocument("OWN");
        host.Db.DocumentLinks.AddRange(new DocumentLink(visible.Id, LinkedEntityType.Worker, own.Id), new(visible.Id, LinkedEntityType.Worker, other.Id));
        var deletedLinkDocument = host.AddDocument("DELETED-LINK");
        var removed = new DocumentLink(deletedLinkDocument.Id, LinkedEntityType.Worker, own.Id);
        removed.MarkAsDeleted();
        host.Db.DocumentLinks.Add(removed);
        var otherDocument = host.AddDocument("OTHER-WORKER");
        host.Db.DocumentLinks.Add(new(otherDocument.Id, LinkedEntityType.Worker, other.Id));
        host.AddDocument("UNLINKED");
        await host.SaveAsync();
        host.Access.Value = host.Access.Value with { OwnOnly = true, WorkerId = own.Id };
        var page = await host.JsonAsync("/api/v1/browser/documents");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        var doc = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(own.Id.ToString(), Assert.Single(doc.GetProperty("workerIds").EnumerateArray()).GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{otherDocument.Id}")).StatusCode);
        host.Access.Value = host.Access.Value with { WorkerId = null };
        Assert.Equal(0, (await host.JsonAsync("/api/v1/browser/documents")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Versions_AreBoundToVisibleDocumentAndNeverInventSnapshotsOrStorageUrls()
    {
        await using var host = await Host.StartAsync();
        var doc = host.AddDocument("VISIBLE");
        var other = host.AddDocument("OTHER", unit: host.OtherUnitId);
        var version = host.AddVersion(doc);
        var foreign = host.AddVersion(other);
        await host.SaveAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{doc.Id}/versions/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/documents/{other.Id}/versions/{foreign.Id}")).StatusCode);
        var current = await host.JsonAsync($"/api/v1/browser/documents/{doc.Id}/versions/{version.Id}");
        Assert.Equal(JsonValueKind.Null, current.GetProperty("metadataSnapshot").ValueKind);
        Assert.Equal(JsonValueKind.Null, current.GetProperty("revision").ValueKind);
        Assert.Equal("Motivo registrado", current.GetProperty("reason").GetString());
        Assert.True(current.GetProperty("sourceReadOnly").GetBoolean());
        Assert.Empty(current.GetProperty("availableActions").EnumerateArray());
        Assert.Empty(current.GetProperty("signatureEvidences").EnumerateArray());
        var file = current.GetProperty("file");
        Assert.Equal("CLEAN", file.GetProperty("scanStatus").GetString());
        Assert.False(file.TryGetProperty("objectKey", out _));
        Assert.False(file.TryGetProperty("bucket", out _));
        Assert.False(file.TryGetProperty("url", out _));
        Assert.Equal(1, (await host.JsonAsync($"/api/v1/browser/documents/{doc.Id}/versions?pageSize=1")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Versions_KeepScanFailuresAndRejectedStateAndMissingFileExplicit()
    {
        await using var host = await Host.StartAsync();
        var doc = host.AddDocument("VISIBLE");
        var rejected = host.AddVersion(doc, scan: AntiVirusScanResult.Error);
        rejected.SubmitForReview();
        rejected.Reject(host.UserId, "Arquivo incorreto");
        var missing = new DocumentVersion(doc.Id, 2, Guid.NewGuid(), "hash", host.UserId);
        host.Db.DocumentVersions.Add(missing);
        await host.SaveAsync();
        var current = await host.JsonAsync($"/api/v1/browser/documents/{doc.Id}/versions/{rejected.Id}");
        Assert.Equal("REJECTED", current.GetProperty("status").GetString());
        Assert.Equal("ERROR", current.GetProperty("file").GetProperty("scanStatus").GetString());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("approval").ValueKind);
        var unavailable = await host.JsonAsync($"/api/v1/browser/documents/{doc.Id}/versions/{missing.Id}");
        Assert.Equal(JsonValueKind.Null, unavailable.GetProperty("file").ValueKind);
    }

    [Fact]
    public async Task History_ReturnsOnlyRealEventsForDocumentAndItsVersionsWithoutPayloadLeaks()
    {
        await using var host = await Host.StartAsync();
        var doc = host.AddDocument("VISIBLE");
        var other = host.AddDocument("OTHER");
        var version = host.AddVersion(doc);
        host.Db.AuditLogs.AddRange(
            AuditLog.ForCreate(AuditResource.Document, doc.Id, host.UserId, "private@example.test", "Ana", "{\"medicalSecret\":true}", null),
            AuditLog.ForCreate(AuditResource.DocumentVersion, version.Id, null, null, null, "{}", null),
            AuditLog.ForCreate(AuditResource.Document, other.Id, host.UserId, null, "Other", "{}", null));
        await host.SaveAsync();
        var history = await host.JsonAsync($"/api/v1/browser/documents/{doc.Id}/history?pageSize=1");
        Assert.Equal(2, history.GetProperty("total").GetInt32());
        var entry = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal(doc.Id.ToString(), entry.GetProperty("documentId").GetString());
        Assert.False(entry.TryGetProperty("newData", out _));
        Assert.False(entry.TryGetProperty("changes", out _));
        Assert.False(entry.GetProperty("actor").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task TypeCatalogue_IsReadOnlyAndDoesNotManufactureUploadOrApprovalPolicies()
    {
        await using var host = await Host.StartAsync();
        var deleted = new DocumentType("OLD", "Inativo", null, "SST", SecrecyLevel.Internal, false, false, null, null, "{}", 1);
        deleted.Deactivate();
        host.Db.DocumentTypes.Add(deleted);
        await host.SaveAsync();
        var catalogue = await host.JsonAsync("/api/v1/browser/document-types?q=procedimento&pageSize=1");
        Assert.Equal(1, catalogue.GetProperty("total").GetInt32());
        var type = Assert.Single(catalogue.GetProperty("items").EnumerateArray());
        Assert.True(type.GetProperty("sourceReadOnly").GetBoolean());
        Assert.True(type.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal(JsonValueKind.Null, type.GetProperty("workflow").ValueKind);
        Assert.Equal(JsonValueKind.Null, type.GetProperty("maxFileSizeBytes").ValueKind);
        Assert.Equal(JsonValueKind.Null, type.GetProperty("renewalPolicy").ValueKind);
        Assert.Equal(host.Type.Id.ToString(), (await host.JsonAsync($"/api/v1/browser/document-types/{host.Type.Id}")).GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/browser/document-types/{deleted.Id}")).StatusCode);
    }

    private sealed class AccessReader : IBrowserWorkspaceAccessReader
    {
        public required BrowserWorkspaceAccess Value { get; set; }
        public Task<BrowserWorkspaceAccess?> ReadAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<BrowserWorkspaceAccess?>(userId == Value.UserId ? Value : null);
    }

    // Projection tests isolate the already-tested native policy and verify its intersection.
    private sealed class DocumentPolicy : IDocumentAccessPolicy
    {
        public Guid? DeniedDocument { get; set; }
        public Task<Expression<Func<Document, bool>>> ReadFilterAsync(Guid actorId, CancellationToken ct)
        {
            var denied = DeniedDocument;
            return Task.FromResult<Expression<Func<Document, bool>>>(document => !denied.HasValue || document.Id != denied.Value);
        }
        public Task<bool> CanAccessAsync(Guid actorId, Document document, string action, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ValidateContextAsync(Guid actorId, Document document, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> CanReviewAsync(Guid actorId, Document document, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> CanGrantAsync(Guid actorId, Document document, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> CanReceiveGrantAsync(Guid userId, Document document, CancellationToken ct) => throw new NotSupportedException();
        public Task AddLinksAsync(Guid documentId, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct) => throw new NotSupportedException();
        public Task AddGrantAsync(DocumentAccessGrant grant, CancellationToken ct) => throw new NotSupportedException();
        public Task<DocumentAccessGrant?> GetGrantAsync(Guid grantId, Guid documentId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Host : IAsyncDisposable
    {
        private const string SigningKey = "browser-documents-tests-signing-key-which-is-long-enough-2026";
        private WebApplication app = null!;
        public HttpClient Client { get; private set; } = null!;
        public SesmtDbContext Db { get; private set; } = null!;
        public AccessReader Access { get; private set; } = null!;
        public DocumentPolicy Policy { get; } = new();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid OtherCompanyId { get; } = Guid.NewGuid();
        public Guid UnitId { get; } = Guid.NewGuid();
        public Guid OtherUnitId { get; } = Guid.NewGuid();
        public Guid SectorId { get; } = Guid.NewGuid();
        public Guid OtherSectorId { get; } = Guid.NewGuid();
        public DocumentType Type { get; } = new("PROC", "Procedimento", null, "SST", SecrecyLevel.Internal, false, true, 365, 5, "{}", 3);

        public static async Task<Host> StartAsync()
        {
            var host = new Host();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            var database = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<SesmtDbContext>(options => options.UseInMemoryDatabase(database));
            host.Access = new AccessReader { Value = new(host.UserId, [host.CompanyId], [host.UnitId], [host.SectorId], [], [host.UnitId], false, null,
                [new("documents.read", host.CompanyId)], new Dictionary<Guid, Guid> { [host.UnitId] = host.CompanyId },
                new Dictionary<Guid, Guid> { [host.SectorId] = host.UnitId }) };
            builder.Services.AddSingleton<IBrowserWorkspaceAccessReader>(host.Access);
            builder.Services.AddSingleton<IDocumentAccessPolicy>(host.Policy);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = "documents-tests", ValidateAudience = true, ValidAudience = "browser",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(BrowserDocumentsController).Assembly);
            host.app = builder.Build();
            host.app.UsePathBase("/api/v1");
            host.app.UseAuthentication();
            host.app.UseAuthorization();
            host.app.MapControllers();
            await host.app.StartAsync();
            host.Client = host.app.GetTestClient();
            host.Db = new SesmtDbContext(new DbContextOptionsBuilder<SesmtDbContext>().UseInMemoryDatabase(database).Options);
            host.Db.DocumentTypes.Add(host.Type);
            await host.Db.SaveChangesAsync();
            host.SignIn();
            return host;
        }

        public void SignIn()
        {
            var token = new JwtSecurityToken("documents-tests", "browser", [new Claim("sub", UserId.ToString())],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }

        public Document AddDocument(string code, Guid? company = null, Guid? unit = null, Guid? sector = null,
            string? title = null, string secrecy = SecrecyLevel.Internal, DateOnly? validUntil = null, DocumentType? type = null, bool companyWide = false)
        {
            var document = new Document((type ?? Type).Id, company ?? CompanyId, companyWide ? null : unit ?? UnitId,
                companyWide ? null : sector ?? SectorId, code, title ?? code, null, secrecy, new(2026, 10, 1), null, validUntil, UserId);
            Db.Documents.Add(document);
            return document;
        }

        public Worker AddWorker()
        {
            var worker = new Worker(CompanyId, Guid.NewGuid().ToString(), [1], Guid.NewGuid().ToString(), "Pessoa do teste",
                new(1990, 1, 1), new(2020, 1, 1), null, null, UnitId, SectorId, null);
            Db.Workers.Add(worker);
            return worker;
        }

        public DocumentVersion AddVersion(Document document, string scan = AntiVirusScanResult.Clean)
        {
            var file = new StoredFile("private", "do-not-expose", "private/key", "procedimento.pdf", "application/pdf", 100, "hash", true, scan);
            Db.StoredFiles.Add(file);
            var version = new DocumentVersion(document.Id, 1, file.Id, "hash", UserId, "Motivo registrado");
            Db.DocumentVersions.Add(version);
            return version;
        }

        public Task SaveAsync() => Db.SaveChangesAsync();
        public async Task<JsonElement> JsonAsync(string path)
        {
            var response = await Client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Db.DisposeAsync();
            await app.DisposeAsync();
        }
    }
}
