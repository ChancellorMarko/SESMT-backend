using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Infrastructure.Persistence;
using System.Text.Json;

namespace SESMT.API.Controllers;

public sealed record BindTrainingEvidence(Guid UnitId, int ExpectedRevision, Guid DocumentId, Guid VersionId, int ExpectedDocumentRevision, string Reason, bool Confirmation);
public sealed record UnlinkTrainingEvidence(Guid UnitId, int ExpectedRevision, string Reason, bool Confirmation);
public sealed record TrainingEvidenceOption
{
    public Guid DocumentId { get; init; }
    public Guid VersionId { get; init; }
    public int DocumentRevision { get; init; }
    public int VersionNumber { get; init; }
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string FileHash { get; init; } = "";
}

/// <summary>Administrative attachment of an already published file. Never certifies training or releases work.</summary>
[ApiController, Authorize, Route("trainings/{trainingId:guid}/evidence")]
public sealed class TrainingEvidenceController(SesmtDbContext db, NativeResourceAccess access, IDocumentAccessPolicy documents,
    ICurrentUserService actor, DocumentFileOptions fileOptions) : ApiControllerBase
{
    private Guid Actor => actor.UserId ?? throw new UnauthorizedAccessException();
    private long MaxBytes => Math.Min(fileOptions.MaxSizeBytes, 50 * 1024 * 1024);
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    [HttpGet]
    public async Task<IActionResult> Get(Guid trainingId, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var training = await Load(trainingId, unitId, false, ct);
        return Ok(await Receipt(training, ct));
    }
    [HttpGet("options")]
    public async Task<IActionResult> Options(Guid trainingId, Guid unitId, CancellationToken ct, string? q = null, int page = 1)
    {
        Response.Headers.CacheControl = "no-store";
        if (q?.Length > 160 || page is < 1 or > 1000000) return Failure(400, "VALIDATION_FAILED", "Busca ou página inválida.");
        var training = await Load(trainingId, unitId, true, ct);
        var query = await Eligible(training, ct);
        if (!string.IsNullOrWhiteSpace(q)) { var text = q.Trim().ToLower(); query = query.Where(d => d.Title.ToLower().Contains(text) || d.Code.ToLower().Contains(text)); }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.Title).ThenBy(d => d.DocumentId).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return Ok(new { trainingId, unitId, trainingRevision = training.Revision, items, total, page, pageSize = 20 });
    }

    [HttpPost]
    public async Task<IActionResult> Bind(Guid trainingId, BindTrainingEvidence request, CancellationToken ct)
    {
        if (!Valid(request.Confirmation, request.ExpectedRevision, request.Reason) || request.ExpectedDocumentRevision < 1
            || request.DocumentId == Guid.Empty || request.VersionId == Guid.Empty) return Failure(400, "VALIDATION_FAILED", "Confira versão, motivo e confirmação do vínculo.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var training = await Load(trainingId, request.UnitId, true, ct);
        if (training.Revision != request.ExpectedRevision) return Changed();
        var eligible = await (await Eligible(training, ct)).SingleOrDefaultAsync(d => d.DocumentId == request.DocumentId && d.VersionId == request.VersionId, ct);
        if (eligible is null) return Failure(404, "EVIDENCE_UNAVAILABLE", "Comprovante indisponível para este trabalhador e unidade.");
        // Serialize publication, metadata, version and file changes while binding this exact snapshot.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM docs.documento_tipo WHERE id=(SELECT tipo_id FROM docs.documento WHERE id={request.DocumentId}) FOR SHARE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM docs.documento_versao WHERE id={request.VersionId} FOR SHARE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM docs.arquivo_objeto WHERE id=(SELECT arquivo_id FROM docs.documento_versao WHERE id={request.VersionId}) FOR SHARE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM docs.documento WHERE id={request.DocumentId} FOR UPDATE", ct);
        eligible = await (await Eligible(training, ct)).SingleOrDefaultAsync(d => d.DocumentId == request.DocumentId && d.VersionId == request.VersionId, ct);
        if (eligible is null || eligible.DocumentRevision != request.ExpectedDocumentRevision) return Changed();
        if (training.EvidenceVersionId == eligible.VersionId && training.EvidenceDocumentRevision == eligible.DocumentRevision && training.EvidenceTrainingRevision == training.Revision)
            return Failure(409, "ALREADY_LINKED", "Esta versão já está vinculada a esta revisão do treinamento.");
        var before = Binding(training);
        training.BindEvidence(eligible.DocumentId, eligible.VersionId, eligible.VersionNumber, eligible.DocumentRevision, eligible.FileHash, Actor, DateTimeOffset.UtcNow);
        await Audit("TRAINING_EVIDENCE_LINKED", training, before, request.Reason, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Changed(); }
        return Ok(await Receipt(training, ct));
    }

    [HttpPost("unlink")]
    public async Task<IActionResult> Unlink(Guid trainingId, UnlinkTrainingEvidence request, CancellationToken ct)
    {
        if (!Valid(request.Confirmation, request.ExpectedRevision, request.Reason)) return Failure(400, "VALIDATION_FAILED", "Confirme o motivo e a revisão do vínculo.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var training = await Load(trainingId, request.UnitId, true, ct);
        if (training.Revision != request.ExpectedRevision || !training.EvidenceDocumentId.HasValue) return Changed();
        var before = Binding(training); training.UnbindEvidence(Actor);
        await Audit("TRAINING_EVIDENCE_REMOVED", training, before, request.Reason, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Changed(); }
        return Ok(await Receipt(training, ct));
    }

    private async Task<TrainingRecord> Load(Guid id, Guid unitId, bool manage, CancellationToken ct)
    {
        var unit = await access.UnitAsync(unitId, "treinamento", manage ? "editar" : "ler", ct);
        var scope = await access.RequireAsync("treinamento:ler", unit.CompanyId, false, ct);
        if (manage) await access.RequireAsync("documento:ler", unit.CompanyId, false, ct);
        var sectors = scope.SectorIds.ToArray();
        var record = await db.TrainingRecords.SingleOrDefaultAsync(t => t.Id == id && t.CompanyId == unit.CompanyId && t.UnitId == unitId && t.Active && t.DeletedAt == null
            && (!scope.OwnOnly || t.WorkerId == scope.WorkerId)
            && (scope.FullUnitIds.Contains(unitId) || db.Workers.Any(w => w.Id == t.WorkerId && w.DepartmentId.HasValue && sectors.Contains(w.DepartmentId.Value))), ct)
            ?? throw new SESMT.Application.Exceptions.NotFoundException("Treinamento não encontrado.");
        if (manage && record.RevokedAt.HasValue) throw new ArgumentException("Este treinamento foi invalidado.");
        return record;
    }
    private async Task<IQueryable<TrainingEvidenceOption>> Eligible(TrainingRecord training, CancellationToken ct)
    {
        var filter = await documents.ReadFilterAsync(Actor, ct); var today = Today; var maxBytes = MaxBytes;
        return from doc in db.Documents.AsNoTracking().Where(filter)
            join type in db.DocumentTypes.AsNoTracking() on doc.DocumentTypeId equals type.Id
            join version in db.DocumentVersions.AsNoTracking() on doc.Id equals version.DocumentId
            join file in db.StoredFiles.AsNoTracking() on version.StoredFileId equals file.Id
            where doc.CompanyId == training.CompanyId && doc.UnitId == training.UnitId && doc.Active && doc.DeletedAt == null
                && (doc.SecrecyLevel == SecrecyLevel.Public || doc.SecrecyLevel == SecrecyLevel.Internal)
                && (type.SecrecyLevel == SecrecyLevel.Public || type.SecrecyLevel == SecrecyLevel.Internal)
                && type.Active && type.DeletedAt == null && !type.RequiresSignature && doc.Status == DocumentStatus.Active
                && (!doc.ValidFromDate.HasValue || doc.ValidFromDate <= today) && (!doc.ValidUntilDate.HasValue || doc.ValidUntilDate >= today)
                && version.Active && version.DeletedAt == null && version.IsCurrent && version.State == DocumentVersionState.Current && version.VersionNumber == doc.CurrentVersionNumber
                && file.Active && file.DeletedAt == null && file.Size > 0 && file.Size <= maxBytes && file.FileHash == version.FileHash
                && System.Text.RegularExpressions.Regex.IsMatch(version.FileHash, "^[a-f0-9]{64}$")
                && (file.MimeType == "application/pdf" || file.MimeType == "image/png" || file.MimeType == "image/jpeg")
                && file.AntivirusScanResult != "INFECTED" && file.AntivirusScanResult != "INFECTADO" && file.AntivirusScanResult != "BLOQUEADO"
                && db.DocumentLinks.Any(l => l.DocumentId == doc.Id && l.EntityType == LinkedEntityType.Worker && l.EntityId == training.WorkerId && l.Active && l.DeletedAt == null)
                && !db.DocumentLinks.Any(l => l.DocumentId == doc.Id && l.EntityType == LinkedEntityType.Worker && l.EntityId != training.WorkerId && l.Active && l.DeletedAt == null)
            select new TrainingEvidenceOption { DocumentId = doc.Id, VersionId = version.Id, DocumentRevision = doc.Revision, VersionNumber = version.VersionNumber, Code = doc.Code, Title = doc.Title, FileHash = version.FileHash };
    }
    private async Task<object> Receipt(TrainingRecord training, CancellationToken ct)
    {
        var scope = await access.ReadAsync(ct);
        var canManage = !scope.OwnOnly && !training.RevokedAt.HasValue && scope.FullUnitIds.Contains(training.UnitId)
            && scope.Can("treinamento:editar", training.CompanyId) && scope.Can("documento:ler", training.CompanyId);
        object Result(string state, object? evidence = null) => new { trainingId = training.Id, unitId = training.UnitId, trainingRevision = training.Revision, canManage, state, evidence };
        if (!training.EvidenceDocumentId.HasValue) return Result("EMPTY");
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == training.EvidenceDocumentId && d.Active && d.DeletedAt == null, ct);
        if (doc is null || doc.CompanyId != training.CompanyId || doc.UnitId != training.UnitId
            || doc.SecrecyLevel is not (SecrecyLevel.Public or SecrecyLevel.Internal) || !await documents.CanAccessAsync(Actor, doc, "ler", ct)) return Result("UNAVAILABLE");
        var type = await db.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == doc.DocumentTypeId && t.Active && t.DeletedAt == null, ct);
        var version = await db.DocumentVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == training.EvidenceVersionId && v.DocumentId == doc.Id && v.Active && v.DeletedAt == null, ct);
        if (type is null || type.SecrecyLevel is not (SecrecyLevel.Public or SecrecyLevel.Internal) || version is null
            || version.FileHash != training.EvidenceFileHash || version.VersionNumber != training.EvidenceVersionNumber) return Result("UNAVAILABLE");
        var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == version.StoredFileId && f.Active && f.DeletedAt == null, ct);
        if (file is null || file.FileHash != training.EvidenceFileHash || file.Size <= 0 || file.Size > MaxBytes
            || file.MimeType is not ("application/pdf" or "image/png" or "image/jpeg") || file.AntivirusScanResult is "INFECTED" or "INFECTADO" or "BLOQUEADO"
            || !await db.DocumentLinks.AnyAsync(l => l.DocumentId == doc.Id && l.EntityType == LinkedEntityType.Worker && l.EntityId == training.WorkerId && l.Active && l.DeletedAt == null, ct)
            || await db.DocumentLinks.AnyAsync(l => l.DocumentId == doc.Id && l.EntityType == LinkedEntityType.Worker && l.EntityId != training.WorkerId && l.Active && l.DeletedAt == null, ct)) return Result("UNAVAILABLE");
        var state = training.RevokedAt.HasValue ? "REVOKED" : type.RequiresSignature ? "SIGNATURE_PENDING"
            : doc.Status != DocumentStatus.Active || doc.Revision != training.EvidenceDocumentRevision || !version.IsCurrent || version.State != DocumentVersionState.Current || version.VersionNumber != doc.CurrentVersionNumber ? "DOCUMENT_CHANGED"
            : doc.ValidUntilDate < Today || doc.ValidFromDate > Today ? "EXPIRED" : training.Revision != training.EvidenceTrainingRevision ? "TRAINING_CHANGED" : "CURRENT";
        return Result(state, new { documentId = doc.Id, versionId = version.Id, versionNumber = version.VersionNumber, documentRevision = training.EvidenceDocumentRevision,
            code = doc.Code, title = doc.Title, fileHash = training.EvidenceFileHash, linkedTrainingRevision = training.EvidenceTrainingRevision, linkedAt = training.EvidenceLinkedAt });
    }
    private static bool Valid(bool confirmation, int revision, string? reason) => confirmation && revision > 0 && reason?.Trim().Length is >= 3 and <= 200;
    private IActionResult Changed() => Failure(409, "EVIDENCE_CHANGED", "O treinamento ou o comprovante mudou. Atualize as consultas antes de repetir.");
    private static object Binding(TrainingRecord t) => new { t.EvidenceDocumentId, t.EvidenceVersionId, t.EvidenceVersionNumber, t.EvidenceDocumentRevision, t.EvidenceFileHash, t.EvidenceTrainingRevision, t.EvidenceLinkedAt };
    private async Task Audit(string action, TrainingRecord training, object previous, string reason, CancellationToken ct)
    {
        async Task<string> Json(object value) => await db.Database.SqlQuery<string>($"SELECT CAST(CAST({JsonSerializer.Serialize(value)} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
        var before = await Json(previous); var after = await Json(new { binding = Binding(training), reason = reason.Trim() });
        db.Add(new AuditLog(action, "treinamento", training.Id, Actor, actor.Email, actor.Name, before, after, null, actor.IpAddress, actor.UserAgent,
            await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct), companyId: training.CompanyId, resourceVersion: training.Revision, result: "SUCCESS", traceId: actor.TraceId));
    }
}
