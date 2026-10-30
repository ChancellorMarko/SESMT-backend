using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Contracts.Browser;
using SESMT.API.Services;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

/// <summary>Read-only browser projection; native workflow and storage contracts stay separate.</summary>
[Authorize]
[ApiController]
[Route("browser/documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BrowserDocumentsController(
    SesmtDbContext db,
    IBrowserWorkspaceAccessReader accessReader,
    IDocumentAccessPolicy documentAccess) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] BrowserDocumentQuery query, CancellationToken ct)
    {
        if (ValidateQuery(query) is { } invalid) return invalid;
        if (query.ResponsibleId.HasValue)
            return StatusCode(400, new BrowserErrorResponse("UNSUPPORTED_FILTER", "A fonte de documentos não informa responsáveis. O proprietário está disponível no detalhe.", HttpContext.TraceIdentifier));
        var access = await ReadAccessAsync(ct);
        if (access is null) return AccessDenied();
        var companies = ReadableCompanies(access);
        if (companies.Length == 0) return AccessDenied();
        Guid? selectedCompany = null;
        if (query.UnitId is { } unit)
        {
            if (!access.HasUnit(unit)) return AccessDenied();
            selectedCompany = access.UnitCompanies is not null && access.UnitCompanies.TryGetValue(unit, out var parent)
                ? parent : await db.Units.AsNoTracking().Where(u => u.Id == unit && u.Active && u.DeletedAt == null)
                    .Select(u => (Guid?)u.CompanyId).SingleOrDefaultAsync(ct);
            if (!selectedCompany.HasValue || !access.Can("documents.read", selectedCompany.Value)) return AccessDenied();
        }

        var source = await ReadableDocumentsAsync(access, ct);
        if (selectedCompany.HasValue) source = source.Where(d => d.CompanyId == selectedCompany.Value);
        if (query.UnitId is { } unitId)
            source = source.Where(d => d.UnitId == unitId || d.UnitId == null);
        if (query.TypeId is { } typeId) source = source.Where(d => d.DocumentTypeId == typeId);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim().ToLowerInvariant();
            source = source.Where(d => d.Title.ToLower().Contains(text) || d.Code.ToLower().Contains(text));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var nativeStatus = NativeStatus(query.Status);
            source = source.Where(d => d.Status == nativeStatus);
        }
        if (query.ValidUntilFrom is { } validFrom) source = source.Where(d => d.ValidUntilDate >= validFrom);
        if (query.ValidUntilTo is { } validTo) source = source.Where(d => d.ValidUntilDate <= validTo);
        if (query.IssueDateFrom is { } issuedFrom) source = source.Where(d => d.IssueDate >= issuedFrom);
        if (query.IssueDateTo is { } issuedTo) source = source.Where(d => d.IssueDate <= issuedTo);
        if (query.WorkerId is { } workerId)
        {
            var worker = await db.Workers.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workerId && w.Active && w.DeletedAt == null, ct);
            if (worker is null || !access.Can("documents.read", worker.CompanyId)
                || !access.AllowsLocation(worker.CompanyId, worker.UnitId, worker.DepartmentId)
                || access.OwnOnly && access.WorkerId != workerId
                || query.UnitId.HasValue && worker.UnitId != query.UnitId) return AccessDenied();
            source = source.Where(d => db.DocumentLinks.Any(l => l.Active && l.DeletedAt == null
                && l.DocumentId == d.Id && l.EntityType == LinkedEntityType.Worker && l.EntityId == workerId));
        }
        source = RestrictToOwnWorker(source, access);

        // Location authorization includes direct scope provenance, not only expanded ancestor IDs.
        var candidates = await source.OrderBy(d => d.Code).ThenBy(d => d.Id).ToListAsync(ct);
        var visible = candidates.Where(d => access.AllowsLocation(d.CompanyId, d.UnitId, d.DepartmentId)).ToList();
        var selected = visible.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return Ok(new BrowserDocumentPageResponse<BrowserDocumentResponse>(
            await MapDocumentsAsync(selected, access, ct), query.Page, query.PageSize, visible.Count));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var access = await ReadAccessAsync(ct);
        if (access is null || ReadableCompanies(access).Length == 0) return AccessDenied();
        var document = await FindVisibleDocumentAsync(id, access, ct);
        if (document is null) return MissingDocument();
        return Ok((await MapDocumentsAsync([document], access, ct))[0]);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!ValidPage(page, pageSize)) return InvalidQuery("Informe uma página válida e um limite entre 1 e 100.");
        var access = await ReadAccessAsync(ct);
        if (access is null || ReadableCompanies(access).Length == 0) return AccessDenied();
        if (await FindVisibleDocumentAsync(id, access, ct) is null) return MissingDocument();
        var source = db.DocumentVersions.AsNoTracking().Where(v => v.DocumentId == id && v.Active && v.DeletedAt == null);
        var total = await source.CountAsync(ct);
        var versions = await source.OrderByDescending(v => v.VersionNumber).ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return Ok(new BrowserDocumentPageResponse<BrowserDocumentVersionResponse>(
            await MapVersionsAsync(versions, ct), page, pageSize, total));
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> Version(Guid id, Guid versionId, CancellationToken ct)
    {
        var access = await ReadAccessAsync(ct);
        if (access is null || ReadableCompanies(access).Length == 0) return AccessDenied();
        if (await FindVisibleDocumentAsync(id, access, ct) is null) return MissingDocument();
        var version = await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(
            v => v.Id == versionId && v.DocumentId == id && v.Active && v.DeletedAt == null, ct);
        if (version is null) return MissingDocument();
        return Ok((await MapVersionsAsync([version], ct))[0]);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!ValidPage(page, pageSize)) return InvalidQuery("Informe uma página válida e um limite entre 1 e 100.");
        var access = await ReadAccessAsync(ct);
        if (access is null || ReadableCompanies(access).Length == 0) return AccessDenied();
        if (await FindVisibleDocumentAsync(id, access, ct) is null) return MissingDocument();
        var versionIds = await db.DocumentVersions.AsNoTracking().Where(v => v.DocumentId == id).Select(v => v.Id).ToArrayAsync(ct);
        var source = db.AuditLogs.AsNoTracking().Where(a => a.ResourceId.HasValue &&
            ((a.ResourceType == AuditResource.Document && a.ResourceId == id)
            || (a.ResourceType == AuditResource.DocumentVersion && versionIds.Contains(a.ResourceId.Value))));
        var total = await source.CountAsync(ct);
        var logs = await source.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        // Audit payloads may contain confidential previous metadata; expose event identity only.
        return Ok(new BrowserDocumentPageResponse<BrowserDocumentHistoryResponse>(logs.Select(a =>
            new BrowserDocumentHistoryResponse(a.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), id,
                a.ResourceType == AuditResource.DocumentVersion ? a.ResourceId : null, a.Action,
                new(a.ActorId, a.ActorName), a.OccurredAt)).ToArray(), page, pageSize, total));
    }

    [HttpGet("/browser/document-types")]
    public async Task<IActionResult> Types([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null, CancellationToken ct = default)
    {
        if (!ValidPage(page, pageSize) || q?.Length > 200) return InvalidQuery("Filtros ou paginação inválidos.");
        var access = await ReadAccessAsync(ct);
        if (access is null || ReadableCompanies(access).Length == 0) return AccessDenied();
        var source = ReadableTypes();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim().ToLowerInvariant();
            source = source.Where(t => t.Name.ToLower().Contains(text) || t.Code.ToLower().Contains(text));
        }
        var total = await source.CountAsync(ct);
        var types = await source.OrderBy(t => t.Code).ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return Ok(new BrowserDocumentPageResponse<BrowserDocumentTypeResponse>(types.Select(MapType).ToArray(), page, pageSize, total));
    }

    [HttpGet("/browser/document-types/{id:guid}")]
    public async Task<IActionResult> Type(Guid id, CancellationToken ct)
    {
        var access = await ReadAccessAsync(ct);
        if (access is null || ReadableCompanies(access).Length == 0) return AccessDenied();
        var type = await ReadableTypes().SingleOrDefaultAsync(t => t.Id == id, ct);
        return type is null ? MissingDocument() : Ok(MapType(type));
    }

    private IQueryable<DocumentType> ReadableTypes() => db.DocumentTypes.AsNoTracking().Where(t => t.Active && t.DeletedAt == null
        && (t.SecrecyLevel == SecrecyLevel.Public || t.SecrecyLevel == SecrecyLevel.Internal));

    private IQueryable<Document> ReadableDocuments(Guid[] companies) => db.Documents.AsNoTracking().Where(d =>
        d.Active && d.DeletedAt == null && companies.Contains(d.CompanyId)
        && (d.SecrecyLevel == SecrecyLevel.Public || d.SecrecyLevel == SecrecyLevel.Internal)
        && db.DocumentTypes.Any(t => t.Id == d.DocumentTypeId && t.Active && t.DeletedAt == null
            && (t.SecrecyLevel == SecrecyLevel.Public || t.SecrecyLevel == SecrecyLevel.Internal)));

    // The browser projection can narrow the native document policy, never replace it.
    // In particular, leader/worker restrictions remain effective even for internal documents.
    private async Task<IQueryable<Document>> ReadableDocumentsAsync(BrowserWorkspaceAccess access, CancellationToken ct) =>
        ReadableDocuments(ReadableCompanies(access)).Where(await documentAccess.ReadFilterAsync(access.UserId, ct));

    private IQueryable<Document> RestrictToOwnWorker(IQueryable<Document> source, BrowserWorkspaceAccess access)
    {
        if (!access.OwnOnly) return source;
        if (access.WorkerId is not { } workerId) return source.Where(_ => false);
        return source.Where(d => db.DocumentLinks.Any(l => l.Active && l.DeletedAt == null && l.DocumentId == d.Id
            && l.EntityType == LinkedEntityType.Worker && l.EntityId == workerId));
    }

    private async Task<Document?> FindVisibleDocumentAsync(Guid id, BrowserWorkspaceAccess access, CancellationToken ct)
    {
        var document = await RestrictToOwnWorker(await ReadableDocumentsAsync(access, ct), access)
            .SingleOrDefaultAsync(d => d.Id == id, ct);
        return document is not null && access.AllowsLocation(document.CompanyId, document.UnitId, document.DepartmentId)
            ? document : null;
    }

    private async Task<BrowserDocumentResponse[]> MapDocumentsAsync(Document[] documents, BrowserWorkspaceAccess access, CancellationToken ct)
    {
        var ids = documents.Select(d => d.Id).ToArray();
        var versions = await db.DocumentVersions.AsNoTracking().Where(v => ids.Contains(v.DocumentId) && v.Active && v.DeletedAt == null)
            .OrderByDescending(v => v.VersionNumber).ThenBy(v => v.Id).ToArrayAsync(ct);
        var links = await db.DocumentLinks.AsNoTracking().Where(l => ids.Contains(l.DocumentId) && l.Active && l.DeletedAt == null
            && l.EntityType == LinkedEntityType.Worker).ToArrayAsync(ct);
        var linkedIds = links.Select(l => l.EntityId).Distinct().ToArray();
        var workers = await db.Workers.AsNoTracking().Where(w => linkedIds.Contains(w.Id) && w.Active && w.DeletedAt == null).ToArrayAsync(ct);
        var visibleWorkers = workers.Where(w => access.AllowsLocation(w.CompanyId, w.UnitId, w.DepartmentId)
            && (!access.OwnOnly || access.WorkerId == w.Id)).ToDictionary(w => w.Id);
        return documents.Select(d =>
        {
            var documentVersions = versions.Where(v => v.DocumentId == d.Id).ToArray();
            var active = documentVersions.Where(v => v.IsCurrent && v.State == DocumentVersionState.Current).Select(v => v.Id).ToArray();
            return new BrowserDocumentResponse(d.Id, d.Code, d.Title, d.DocumentTypeId, d.CompanyId, d.UnitId,
                d.DepartmentId, d.OwnerId, AccessClass(d.SecrecyLevel), d.IssueDate, d.ValidFromDate, d.ValidUntilDate,
                links.Where(l => l.DocumentId == d.Id && visibleWorkers.TryGetValue(l.EntityId, out var w) && w.CompanyId == d.CompanyId)
                    .Select(l => l.EntityId).Distinct().ToArray(),
                [], d.Keywords, BrowserStatus(d.Status), documentVersions.FirstOrDefault()?.Id,
                active.FirstOrDefault() is var first && first != Guid.Empty ? first : null, active, [], null);
        }).ToArray();
    }

    private async Task<BrowserDocumentVersionResponse[]> MapVersionsAsync(DocumentVersion[] versions, CancellationToken ct)
    {
        var fileIds = versions.Select(v => v.StoredFileId).ToArray();
        var files = await db.StoredFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id) && f.Active && f.DeletedAt == null)
            .ToDictionaryAsync(f => f.Id, ct);
        return versions.Select(v => new BrowserDocumentVersionResponse(v.Id, v.DocumentId, v.VersionNumber,
            v.Justification, BrowserStatus(v.State), null,
            files.TryGetValue(v.StoredFileId, out var f) ? new(f.Id, f.FileName, f.MimeType, f.Size, f.FileHash, ScanStatus(f.AntivirusScanResult)) : null,
            v.CreatedAt, v.AuthorId, v.ApprovedAt.HasValue ? new(v.ApproverId, v.ApprovedAt) : null, [], [], null)).ToArray();
    }

    private static BrowserDocumentTypeResponse MapType(DocumentType type) => new(type.Id, type.Code, type.Name, type.Category,
        AccessClass(type.SecrecyLevel), type.RequiresApproval, type.RequiresSignature, type.PeriodicityInDays,
        type.RetentionPeriodInYears, type.SchemaVersion);

    private static string AccessClass(string secrecy) => secrecy switch
    {
        SecrecyLevel.Public => "PUBLIC", SecrecyLevel.Internal => "INTERNAL",
        SecrecyLevel.Restricted => "RESTRICTED", SecrecyLevel.Medical => "MEDICAL", _ => "UNKNOWN"
    };

    private static string ScanStatus(string scan) => scan switch
    {
        AntiVirusScanResult.Clean => "CLEAN", AntiVirusScanResult.Infected => "REJECTED",
        AntiVirusScanResult.Pending => "PENDING", AntiVirusScanResult.Error => "ERROR", _ => "UNKNOWN"
    };

    private static string BrowserStatus(string status) => status switch
    {
        DocumentStatus.Draft => "DRAFT", DocumentStatus.InReview => "IN_REVIEW",
        DocumentStatus.Approved or DocumentVersionState.Approved => "APPROVED",
        DocumentStatus.Active => "ACTIVE", DocumentStatus.Expired => "EXPIRED",
        DocumentStatus.Superseded or DocumentVersionState.Superseded => "SUPERSEDED",
        DocumentStatus.Archived => "ARCHIVED", DocumentVersionState.Rejected => "REJECTED", _ => "UNKNOWN"
    };

    private static string? NativeStatus(string status) => status switch
    {
        "DRAFT" => DocumentStatus.Draft, "IN_REVIEW" => DocumentStatus.InReview,
        "APPROVED" => DocumentStatus.Approved, "ACTIVE" => DocumentStatus.Active,
        "EXPIRED" => DocumentStatus.Expired, "SUPERSEDED" => DocumentStatus.Superseded,
        "ARCHIVED" => DocumentStatus.Archived, _ => null
    };

    private IActionResult? ValidateQuery(BrowserDocumentQuery query)
    {
        if (!ValidPage(query.Page, query.PageSize) || query.Q?.Length > 200
            || (query.Status is { Length: > 0 } status && NativeStatus(status) is null)
            || query.UnitId == Guid.Empty || query.TypeId == Guid.Empty || query.WorkerId == Guid.Empty || query.ResponsibleId == Guid.Empty
            || (query.ValidUntilFrom.HasValue && query.ValidUntilTo.HasValue && query.ValidUntilFrom > query.ValidUntilTo)
            || (query.IssueDateFrom.HasValue && query.IssueDateTo.HasValue && query.IssueDateFrom > query.IssueDateTo))
            return InvalidQuery("Filtros ou paginação inválidos. Confira o período e os identificadores.");
        return null;
    }

    private static bool ValidPage(int page, int pageSize) => page > 0 && pageSize is > 0 and <= 100
        && (long)(page - 1) * pageSize <= int.MaxValue;
    private Guid[] ReadableCompanies(BrowserWorkspaceAccess access) => access.CompanyIds.Where(id => access.Can("documents.read", id)).ToArray();
    private async Task<BrowserWorkspaceAccess?> ReadAccessAsync(CancellationToken ct) =>
        Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await accessReader.ReadAsync(id, ct) : null;
    private ObjectResult InvalidQuery(string message) => StatusCode(400, new BrowserErrorResponse("VALIDATION_ERROR", message, HttpContext.TraceIdentifier));
    private ObjectResult AccessDenied() => StatusCode(403, new BrowserErrorResponse("FORBIDDEN", "Você não possui acesso a estes documentos.", HttpContext.TraceIdentifier));
    private ObjectResult MissingDocument() => StatusCode(404, new BrowserErrorResponse("NOT_FOUND", "Documento ou versão não encontrado.", HttpContext.TraceIdentifier));
}
