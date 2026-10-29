using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using SESMT.Application.Common;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Application.Modules.Documents.Services;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Documents.Repositories;

namespace SESMT.Tests.Unit.Documents;

public sealed class DocumentSafetyTests
{
    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("versions")]
    [InlineData("download")]
    public async Task UnauthorizedResource_IsHiddenBeforeFileOrVersionAccess(string operation)
    {
        var f = new Fixture { AllowAccess = false };
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => operation switch
        {
            "read" => f.Service.GetByIdAsync(f.Document.Id),
            "write" => f.Service.UpdateAsync(f.Document.Id, new() { Title = "changed" }),
            "versions" => f.Service.ListVersionsAsync(f.Document.Id),
            _ => f.Service.DownloadVersionAsync(f.Document.Id, Guid.NewGuid()),
        });
        Assert.Equal(404, failure.Status);
        Assert.Equal(0, f.StorageCalls);
        Assert.Equal(0, f.VersionReads);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task MissingSession_DoesNotLoadTheResource()
    {
        var f = new Fixture { Authenticated = false };
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.GetByIdAsync(f.Document.Id));
        Assert.Equal(401, failure.Status); Assert.Equal(0, f.DocumentReads);
    }

    [Fact]
    public async Task Search_PassesAuthorizationBeforePaginationAndCount()
    {
        var f = new Fixture();
        f.SearchDocuments.Add(f.Document);
        f.SearchDocuments.Add(new(Guid.NewGuid(), Guid.NewGuid(), null, null, "OUT", "Outra prestadora", null, SecrecyLevel.Internal, new(2026, 1, 1), null, null, f.ActorId));
        f.SearchDocuments.Add(new(Guid.NewGuid(), f.Document.CompanyId, null, null, "MED", "Dado médico", null, SecrecyLevel.Medical, new(2026, 1, 1), null, null, f.ActorId));
        var result = await f.Service.SearchAsync(new() { Page = 1, PageSize = 1 });
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal(f.Document.Id, Assert.Single(result.Value.Items).Id);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task InvalidPagination_IsRejectedBeforeRepository(int page, int size)
    {
        var f = new Fixture();
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.SearchAsync(new() { Page = page, PageSize = size }));
        Assert.Equal(400, failure.Status); Assert.Equal(0, f.SearchCalls);
    }

    [Fact]
    public async Task Pagination_UsesConfiguredMaximum()
    {
        var f = new Fixture(); f.QueryOptions.MaxPageSize = 25;
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.SearchAsync(new() { PageSize = 26 }));
        Assert.Equal(400, failure.Status); Assert.Equal(0, f.SearchCalls);
    }

    [Fact]
    public async Task InvalidContext_DoesNotCreateDocumentOrLinks()
    {
        var f = new Fixture { AllowContext = false };
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.CreateAsync(f.CreateRequest()));
        Assert.Equal(403, failure.Status); Assert.Null(f.AddedDocument); Assert.Empty(f.Links);
    }

    [Fact]
    public async Task ParticipantAlias_CreatesValidatedWorkerLink()
    {
        var f = new Fixture();
        var request = f.CreateRequest(); request.PersonId = Guid.NewGuid();
        await f.Service.CreateAsync(request);
        var link = Assert.Single(f.Links);
        Assert.Equal(LinkedEntityType.Worker, link.EntityType); Assert.Equal(request.PersonId, link.EntityId);
        Assert.Equal(f.ActorId, f.ValidatedActor);
    }

    [Fact]
    public async Task MedicalDocumentType_CannotBeClassifiedAsGeneral()
    {
        var f = new Fixture();
        f.Type.UpdateDocumentType("ASO", "ASO", null, "Saúde", SecrecyLevel.Medical, false, true, null, null, "{}", 1);
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.CreateAsync(f.CreateRequest()));
        Assert.Equal(403, failure.Status); Assert.Null(f.AddedDocument); Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Acceptance_RequiresAvailableUnchangedObject()
    {
        var f = new Fixture(); var version = await f.UploadAsync();
        f.Blobs[f.Files.Single().ObjectKey] = Encoding.ASCII.GetBytes("%PDF-corrupted");
        var revision = f.Document.Revision;
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.ReviewAsync(f.Document.Id, version.Id, new(revision, 1, "aceita", null)));
        Assert.Equal(422, failure.Status); Assert.Equal(revision, f.Document.Revision);
        Assert.Equal(DocumentVersionState.InReview, f.Versions.Single().State);
    }

    [Fact]
    public async Task Upload_UsesSessionActorAndRemainsPendingReview()
    {
        var f = new Fixture();
        var result = await f.UploadAsync(author: Guid.NewGuid());
        Assert.Equal(f.ActorId, result.AuthorId);
        Assert.Equal(DocumentVersionState.InReview, result.State);
        Assert.False(result.IsCurrent); Assert.Null(result.ApproverId);
        Assert.Equal(1, f.Document.CurrentVersionNumber); Assert.Equal(2, f.Document.Revision);
        Assert.Equal(DocumentStatus.InReview, f.Document.Status);
    }

    [Fact]
    public async Task Upload_WithStaleRevisionDoesNotTouchStorage()
    {
        var f = new Fixture();
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.AddVersionAsync(f.Document.Id,
            new() { ExpectedVersion = 99 }, new MemoryStream(Fixture.Pdf), "a.pdf", "application/pdf"));
        Assert.Equal(409, failure.Status); Assert.Equal(0, f.StorageCalls);
    }

    [Theory]
    [InlineData("application/pdf", "a.pdf", "not a pdf", 400)]
    [InlineData("text/html", "a.pdf", "%PDF-1.7 example", 400)]
    [InlineData("application/pdf", "a.exe", "%PDF-1.7 example", 400)]
    public async Task Upload_RejectsContentTypeAndNameMismatch(string mime, string name, string text, int status)
    {
        var f = new Fixture();
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.AddVersionAsync(f.Document.Id,
            new() { ExpectedVersion = 1 }, new MemoryStream(Encoding.ASCII.GetBytes(text)), name, mime));
        Assert.Equal(status, failure.Status); Assert.Equal(0, f.StorageCalls); Assert.Empty(f.Versions);
    }

    [Fact]
    public async Task OversizedOrCancelledUpload_DoesNotCreateStorageReferences()
    {
        var f = new Fixture(); f.Options.MaxSizeBytes = 5;
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.UploadAsync());
        Assert.Equal(413, failure.Status); Assert.Equal(0, f.StorageCalls); Assert.Empty(f.Versions);
        f.Options.MaxSizeBytes = 100;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.AddVersionAsync(f.Document.Id,
            new() { ExpectedVersion = 1 }, new MemoryStream(Fixture.Pdf), "a.pdf", "application/pdf", cancellation.Token));
        Assert.Equal(0, f.StorageCalls); Assert.Empty(f.Versions);
    }

    [Fact]
    public async Task PersistenceFailure_RemovesUnreferencedObject()
    {
        var f = new Fixture { FailSave = true };
        await Assert.ThrowsAsync<IOException>(() => f.UploadAsync());
        Assert.Single(f.DeletedObjects); Assert.Empty(f.Blobs);
        Assert.DoesNotContain(f.Versions, v => v.IsCurrent);
    }

    [Fact]
    public async Task RevocationDuringUpload_RemovesObjectAndDoesNotCreateVersion()
    {
        var f = new Fixture { RevokeOnUpload = true };
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.UploadAsync());
        Assert.Equal(403, failure.Status); Assert.Single(f.DeletedObjects); Assert.Empty(f.Versions);
    }

    [Fact]
    public async Task Review_RequiresDesignationAndDoesNotTrustBodyApprover()
    {
        var f = new Fixture(); var uploaded = await f.UploadAsync();
        f.AllowReview = false;
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.ApproveVersionAsync(f.Document.Id,
            uploaded.Id, new() { ExpectedVersion = f.Document.Revision, ApproverId = Guid.NewGuid() }));
        Assert.Equal(403, failure.Status); Assert.Equal(DocumentVersionState.InReview, f.Versions.Single().State);
        f.AllowReview = true;
        var approved = await f.Service.ApproveVersionAsync(f.Document.Id, uploaded.Id, new() { ExpectedVersion = f.Document.Revision, ApproverId = Guid.NewGuid() });
        Assert.Equal(f.ActorId, approved.Value!.ApproverId);
        Assert.True(approved.Value.IsCurrent); Assert.Equal(DocumentStatus.Active, f.Document.Status);
    }

    [Fact]
    public async Task NewVersion_PreservesOldAcceptedVersionButRequiresNewDecision()
    {
        var f = new Fixture(); var first = await f.UploadAsync();
        await f.Service.ReviewAsync(f.Document.Id, first.Id, new(f.Document.Revision, 1, "aceita", null));
        var second = await f.UploadAsync();
        Assert.True(f.Versions.First().IsCurrent);
        Assert.False(second.IsCurrent); Assert.Equal(DocumentVersionState.InReview, second.State);
        Assert.Equal(2, f.Document.CurrentVersionNumber);
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.ReviewAsync(f.Document.Id, first.Id,
            new(f.Document.Revision, 1, "aceita", null)));
        Assert.Equal(409, failure.Status);
        await f.Service.ReviewAsync(f.Document.Id, second.Id, new(f.Document.Revision, 2, "aceita", null));
        Assert.Equal(DocumentVersionState.Superseded, f.Versions.First().State);
        Assert.True(f.Versions.Last().IsCurrent);
    }

    [Fact]
    public async Task Rejection_RequiresReasonAndRecordsSessionActorAndDecisionTime()
    {
        var f = new Fixture(); var uploaded = await f.UploadAsync();
        var revision = f.Document.Revision;
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.ReviewAsync(f.Document.Id, uploaded.Id, new(revision, 1, "reprovada", " ")));
        Assert.Equal(400, failure.Status); Assert.Equal(revision, f.Document.Revision);
        var result = await f.Service.ReviewAsync(f.Document.Id, uploaded.Id, new(revision, 1, "reprovada", "Evidência ilegível"));
        Assert.Equal(DocumentVersionState.Rejected, result.Value!.State); Assert.Equal(f.ActorId, result.Value.ApproverId);
        Assert.NotNull(result.Value.ReviewedAt); Assert.False(result.Value.IsCurrent);
    }

    [Fact]
    public async Task MedicalClassification_CannotBeDowngradedAndExposeData()
    {
        var f = new Fixture(secrecy: SecrecyLevel.Medical);
        var original = f.Document.Title;
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.UpdateAsync(f.Document.Id,
            new() { Title = "changed", SecrecyLevel = SecrecyLevel.Public, IssueDate = new(2026, 1, 1) }));
        Assert.Equal(403, failure.Status); Assert.Equal(original, f.Document.Title); Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Download_UsesLinkedVersionAndConfirmsIntegrity()
    {
        var f = new Fixture(); var uploaded = await f.UploadAsync();
        var download = (await f.Service.DownloadFileAsync(f.Document.Id, uploaded.StoredFileId)).Value!;
        await using (download.Content) Assert.Equal(Fixture.Pdf.Length, download.Content.Length);
        f.Blobs[f.Files.Single().ObjectKey] = Encoding.ASCII.GetBytes("%PDF-bad bytes");
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.DownloadVersionAsync(f.Document.Id, uploaded.Id));
        Assert.Equal(409, failure.Status);
    }

    [Fact]
    public async Task Grant_RequiresSpecificCapabilityAndTargetScope()
    {
        var f = new Fixture(secrecy: SecrecyLevel.Restricted) { AllowGrant = false };
        var request = new DocumentGrantRequest(Guid.NewGuid(), true, DateTimeOffset.UtcNow.AddHours(1), 1);
        var failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.GrantAsync(f.Document.Id, request));
        Assert.Equal(404, failure.Status); Assert.Empty(f.Grants);
        f.AllowGrant = true; f.AllowRecipient = false;
        failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.GrantAsync(f.Document.Id, request));
        Assert.Equal(403, failure.Status); Assert.Empty(f.Grants);
        f.AllowRecipient = true;
        var grant = (await f.Service.GrantAsync(f.Document.Id, request)).Value!;
        Assert.Equal(f.ActorId, f.Grants.Single().GrantedBy);
        await f.Service.RevokeGrantAsync(f.Document.Id, grant.Id, 1);
        Assert.NotNull(f.Grants.Single().RevokedAt);
        failure = await Assert.ThrowsAsync<DocumentOperationException>(() => f.Service.RevokeGrantAsync(f.Document.Id, grant.Id, 1));
        Assert.Equal(409, failure.Status);
    }

    private sealed class Fixture : IDocumentRepository, IDocumentTypeRepository, IDocumentVersionRepository, IStoredFileRepository,
        IDocumentAccessPolicy, IDocumentSearchRepository, IDocumentVersionWorkflow, IFileStorage, IAuditLogRepository, ICurrentUserService, IUnitOfWork
    {
        public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nfictitious evidence");
        public Guid ActorId { get; } = Guid.NewGuid();
        public Document Document { get; }
        public DocumentType Type { get; } = new("CERT", "Certificado", null, "Geral", SecrecyLevel.Internal, false, true, null, null, "{}", 1);
        public bool AllowAccess = true, AllowContext = true, AllowReview = true, AllowGrant = true, AllowRecipient = true, Authenticated = true, FailSave, RevokeOnUpload;
        public int DocumentReads, VersionReads, StorageCalls, Saves, SearchCalls;
        public Guid? ValidatedActor;
        public Document? AddedDocument;
        public List<DocumentLinkCreateRequest> Links { get; } = [];
        public List<DocumentVersion> Versions { get; } = [];
        public List<StoredFile> Files { get; } = [];
        public List<Document> SearchDocuments { get; } = [];
        public List<DocumentAccessGrant> Grants { get; } = [];
        public Dictionary<string, byte[]> Blobs { get; } = [];
        public List<string> DeletedObjects { get; } = [];
        public DocumentFileOptions Options { get; } = new();
        public DocumentQueryOptions QueryOptions { get; } = new();
        public DocumentService Service { get; }

        public Fixture(string secrecy = SecrecyLevel.Internal)
        {
            Document = new(Type.Id, Guid.NewGuid(), null, null, "DOC", "Evidência", null, secrecy, new(2026, 1, 1), null, null, ActorId);
            Service = new(this, this, this, this, this, this, this, this, this, this, this, Options, queryOptions: QueryOptions);
        }
        public DocumentCreateRequest CreateRequest() => new() { DocumentTypeId = Type.Id, CompanyId = Document.CompanyId, OwnerId = ActorId, Code = "NEW", Title = "Nova evidência", SecrecyLevel = SecrecyLevel.Internal, IssueDate = new(2026, 1, 1) };
        public async Task<DocumentVersionResponse> UploadAsync(Guid? author = null) => (await Service.AddVersionAsync(Document.Id,
            new() { ExpectedVersion = Document.Revision, AuthorId = author ?? ActorId }, new MemoryStream(Pdf), "evidence.pdf", "application/pdf")).Value!;

        public Guid? UserId => ActorId; public string? Email => "fictional@example.test"; public string? Name => "Fictício";
        public string? IpAddress => null; public string? UserAgent => null; public string? TraceId => "test";
        public bool IsAuthenticated => Authenticated;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { if (FailSave) throw new IOException("Simulated persistence failure"); Saves++; return Task.FromResult(1); }

        Task<Document?> IDocumentRepository.GetByIdAsync(Guid id, CancellationToken ct) { DocumentReads++; return Task.FromResult<Document?>(id == Document.Id ? Document : null); }
        public Task<bool> ExistsByCodeAsync(Guid typeId, string code, Guid? unitId, Guid? ignoreId, CancellationToken ct) => Task.FromResult(false);
        Task<IReadOnlyList<Document>> IDocumentRepository.ListAsync(Guid companyId, Guid? unitId, Guid? departmentId, Guid? typeId, string? status, string? secrecy, string? search, DateOnly? before, int skip, int take, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(Document document, CancellationToken ct) { AddedDocument = document; return Task.CompletedTask; }
        public void Update(Document document) { }
        Task<DocumentType?> IDocumentTypeRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<DocumentType?>(id == Type.Id ? Type : null);
        public Task<DocumentType?> GetByCodeAsync(string code, CancellationToken ct) => Task.FromResult<DocumentType?>(Type);
        public Task<bool> ExistsByCodeAsync(string code, Guid? ignoreId, CancellationToken ct) => Task.FromResult(false);
        Task<IReadOnlyList<DocumentType>> IDocumentTypeRepository.ListAsync(string? category, bool? active, CancellationToken ct) => Task.FromResult<IReadOnlyList<DocumentType>>([Type]);
        public Task AddAsync(DocumentType type, CancellationToken ct) => Task.CompletedTask;
        public void Update(DocumentType type) { }
        Task<DocumentVersion?> IDocumentVersionRepository.GetByIdAsync(Guid id, CancellationToken ct) { VersionReads++; return Task.FromResult(Versions.SingleOrDefault(v => v.Id == id)); }
        public Task<DocumentVersion?> GetCurrentByDocumentAsync(Guid id, CancellationToken ct) => Task.FromResult(Versions.SingleOrDefault(v => v.DocumentId == id && v.IsCurrent));
        public Task<int> GetNextVersionNumberAsync(Guid id, CancellationToken ct) => Task.FromResult(Versions.Count + 1);
        public Task<IReadOnlyList<DocumentVersion>> ListByDocumentAsync(Guid id, CancellationToken ct) { VersionReads++; return Task.FromResult<IReadOnlyList<DocumentVersion>>(Versions.Where(v => v.DocumentId == id).ToArray()); }
        public Task AddAsync(DocumentVersion version, CancellationToken ct) { Versions.Add(version); return Task.CompletedTask; }
        public void Update(DocumentVersion version) { }
        Task<StoredFile?> IStoredFileRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Files.SingleOrDefault(f => f.Id == id));
        public Task AddAsync(StoredFile file, CancellationToken ct) { Files.Add(file); return Task.CompletedTask; }
        public void Update(StoredFile file) { }

        public Task<Expression<Func<Document, bool>>> ReadFilterAsync(Guid actorId, CancellationToken ct) => Task.FromResult<Expression<Func<Document, bool>>>(d => AllowAccess && d.CompanyId == Document.CompanyId);
        public Task<bool> CanAccessAsync(Guid actorId, Document document, string action, CancellationToken ct) => Task.FromResult(AllowAccess && actorId == ActorId);
        public Task<bool> ValidateContextAsync(Guid actorId, Document document, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct) { ValidatedActor = actorId; return Task.FromResult(AllowContext); }
        public Task<bool> CanReviewAsync(Guid actorId, Document document, CancellationToken ct) => Task.FromResult(AllowReview);
        public Task<bool> CanGrantAsync(Guid actorId, Document document, CancellationToken ct) => Task.FromResult(AllowGrant);
        public Task<bool> CanReceiveGrantAsync(Guid userId, Document document, CancellationToken ct) => Task.FromResult(AllowRecipient);
        public Task AddLinksAsync(Guid id, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct) { Links.AddRange(links); return Task.CompletedTask; }
        public Task AddGrantAsync(DocumentAccessGrant grant, CancellationToken ct) { Grants.Add(grant); return Task.CompletedTask; }
        public Task<DocumentAccessGrant?> GetGrantAsync(Guid id, Guid documentId, CancellationToken ct) => Task.FromResult(Grants.SingleOrDefault(g => g.Id == id && g.DocumentId == documentId));
        public Task<DocumentPageResponse> SearchAsync(DocumentSearchRequest request, Expression<Func<Document, bool>> authorized, CancellationToken ct)
        {
            SearchCalls++;
            var all = SearchDocuments.Where(authorized.Compile()).Where(d => d.SecrecyLevel is SecrecyLevel.Public or SecrecyLevel.Internal).ToArray();
            return Task.FromResult(new DocumentPageResponse(all.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(DocumentMapper.ToSummary).ToArray(), request.Page, request.PageSize, all.Length));
        }
        public Task ReviewAsync(Document document, DocumentVersion version, Guid actorId, bool accepted, string? reason, CancellationToken ct)
        {
            if (accepted) { foreach (var old in Versions.Where(v => v.IsCurrent)) old.Supersede(); version.Approve(actorId); version.Publish(); }
            else version.Reject(actorId, reason!);
            document.RecordReview(accepted, actorId); return Task.CompletedTask;
        }
        public async Task<FileStorageResult> UploadAsync(Stream input, string name, string mime, CancellationToken ct)
        {
            StorageCalls++; using var buffer = new MemoryStream(); await input.CopyToAsync(buffer, ct); var bytes = buffer.ToArray();
            var key = Guid.NewGuid() + ".pdf"; Blobs[key] = bytes;
            if (RevokeOnUpload) AllowAccess = false;
            return new("FAKE", "test", key, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length);
        }
        public Task<Stream> DownloadAsync(string key, CancellationToken ct) { StorageCalls++; return Task.FromResult<Stream>(new MemoryStream(Blobs[key])); }
        public Task DeleteAsync(string key, CancellationToken ct) { DeletedObjects.Add(key); Blobs.Remove(key); return Task.CompletedTask; }
        public Task AddAsync(AuditLog log, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetLastHashAsync(CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<AuditLog>> QueryAsync(Guid? actor, string? action, string? resource, Guid? id, DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<Guid> companies, int skip, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
        public Task<int> CountAsync(Guid? actor, string? action, string? resource, Guid? id, DateTimeOffset? from, DateTimeOffset? to, IReadOnlyCollection<Guid> companies, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyCollection<Guid>> GetAccessibleCompanyIdsAsync(Guid actor, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([Document.CompanyId]);
        public Task<AuditLog?> GetByEventIdAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct) => Task.FromResult<AuditLog?>(null);
    }
}
