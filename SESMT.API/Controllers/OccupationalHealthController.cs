using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.OccupationalHealth.Entities;
using SESMT.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SESMT.API.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AsoDetails(string Fitness, string EvaluatedFunction, string PhysicianName, string PhysicianCrm, string PhysicianState, string? Reference);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AsoRequest(Guid UnitId, Guid WorkerId, Guid CommandId, string ExamKind, DateOnly ExamOn, DateOnly IssuedOn,
    DateOnly? NextEvaluationOn, AsoDetails Details, bool Confirmation, int? ExpectedRevision, string? ChangeCode);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeAsoRequest(Guid UnitId, int ExpectedRevision, string ReasonCode, bool Confirmation);

[ApiController, Authorize, Route("occupational-health")]
public sealed class OccupationalHealthController(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor,
    IDataProtectionProvider protection) : ApiControllerBase
{
    private readonly IDataProtector protector = protection.CreateProtector("SESMT.OccupationalHealth.v1");
    private Guid Actor => actor.UserId ?? throw new UnauthorizedAccessException();
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);
    private static readonly string[] States = "AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO".Split(' ');
    private bool Editable(BrowserWorkspaceAccess s, Guid company, Guid unit) => !s.OwnOnly && s.FullUnitIds.Contains(unit)
        && new[] { "aso:acompanhar", "aso:detalhar", "aso:editar", "trabalhador:ler" }.All(p => s.Can(p, company));

    [HttpGet("context")]
    public async Task<IActionResult> Context(Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var unit = await access.UnitAsync(unitId, "aso", "acompanhar", ct); var s = await access.ReadAsync(ct);
        var canEdit = Editable(s, unit.CompanyId, unitId);
        var workers = canEdit ? await (await access.WorkersAsync("ler", ct)).Where(w => w.UnitId == unitId)
            .OrderBy(w => w.Name.Value).Select(w => new { w.Id, name = w.Name.Value, w.Enrollment }).Take(500).ToArrayAsync(ct) : [];
        return Ok(new { unitId, companyId = unit.CompanyId, ownOnly = s.OwnOnly, canEdit, canDetail = s.OwnOnly || s.Can("aso:detalhar", unit.CompanyId), workers });
    }

    private async Task<IQueryable<OccupationalCertificate>> Query(Guid unitId, CancellationToken ct)
    {
        var unit = await access.UnitAsync(unitId, "aso", "acompanhar", ct); var s = await access.ReadAsync(ct); var sectors = s.SectorIds.ToArray();
        return db.OccupationalCertificates.AsNoTracking().Where(a => a.UnitId == unitId && a.CompanyId == unit.CompanyId && a.Active && a.DeletedAt == null
            && (!s.OwnOnly || a.WorkerId == s.WorkerId) && db.Workers.Any(w => w.Id == a.WorkerId && w.CompanyId == a.CompanyId && w.UnitId == unitId
                && w.DeletedAt == null && (s.FullUnitIds.Contains(unitId) || w.DepartmentId.HasValue && sectors.Contains(w.DepartmentId.Value))));
    }
    private static IQueryable<Guid> Latest(IQueryable<OccupationalCertificate> query) => query.Where(a => a.RevokedAt == null)
        .GroupBy(a => a.WorkerId).Select(g => g.OrderByDescending(a => a.ExamOn).ThenByDescending(a => a.IssuedOn)
            .ThenByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Select(a => a.Id).First());

    [HttpGet]
    public async Task<IActionResult> List(Guid unitId, CancellationToken ct, string? q = null, string view = "LATEST", string? due = null, int page = 1, int pageSize = 20)
    {
        Response.Headers.CacheControl = "no-store";
        if (q?.Length > 160 || view is not ("LATEST" or "ALL") || due is not (null or "" or "DUE" or "SOON" or "SCHEDULED" or "NO_DATE")
            || page is < 1 or > 1000000 || pageSize is < 1 or > 100) return Failure(400, "VALIDATION_FAILED", "Filtros ou paginação inválidos.");
        var all = await Query(unitId, ct); var latest = Latest(all); var query = view == "LATEST" ? all.Where(a => latest.Contains(a.Id)) : all;
        if (!string.IsNullOrWhiteSpace(q)) { var search = q.Trim().ToLower(); query = query.Where(a => db.Workers.Any(w => w.Id == a.WorkerId
            && (w.Name.Value.ToLower().Contains(search) || w.Enrollment.ToLower().Contains(search)))); }
        var today = Today; var soon = today.AddDays(30);
        if (!string.IsNullOrEmpty(due)) query = query.Where(a => a.RevokedAt == null);
        if (due == "DUE") query = query.Where(a => a.NextEvaluationOn < today);
        if (due == "SOON") query = query.Where(a => a.NextEvaluationOn >= today && a.NextEvaluationOn <= soon);
        if (due == "SCHEDULED") query = query.Where(a => a.NextEvaluationOn > soon);
        if (due == "NO_DATE") query = query.Where(a => a.NextEvaluationOn == null);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(a => a.NextEvaluationOn == null).ThenBy(a => a.NextEvaluationOn).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var rowIds = rows.Select(a => a.Id).ToArray();
        var latestIds = await latest.Where(id => rowIds.Contains(id)).ToArrayAsync(ct); var items = new List<object>();
        foreach (var row in rows) { var result = await View(row, latestIds.Contains(row.Id), ct); if (result == null) return Unavailable(); items.Add(result); }
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var query = await Query(unitId, ct); var item = await query.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (item == null) return Failure(404, "NOT_FOUND", "ASO não encontrado.");
        var result = await View(item, await Latest(query).ContainsAsync(id, ct), ct);
        return result == null ? Unavailable() : Ok(result);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await (await Query(unitId, ct)).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (item == null) return Failure(404, "NOT_FOUND", "ASO não encontrado.");
        var events = await db.AuditLogs.AsNoTracking().Where(a => a.ResourceType == "aso_registro" && a.ResourceId == id && a.CompanyId == item.CompanyId)
            .OrderByDescending(a => a.Id).Take(100).Select(a => new { a.EventId, a.Action, a.ResourceVersion, a.ActorName, a.OccurredAt, a.NewData }).ToArrayAsync(ct);
        return Ok(events.Select(a => { using var json = JsonDocument.Parse(a.NewData!); return new { id = a.EventId, a.Action,
            revision = a.ResourceVersion, a.ActorName, a.OccurredAt, reasonCode = json.RootElement.GetProperty("reasonCode").GetString() }; }));
    }

    [HttpPost] public Task<IActionResult> Create(AsoRequest request, CancellationToken ct) => Save(null, request, ct);
    [HttpPut("{id:guid}")] public Task<IActionResult> Edit(Guid id, AsoRequest request, CancellationToken ct) => Save(id, request, ct);
    private async Task<IActionResult> Save(Guid? id, AsoRequest r, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!r.Confirmation || !Valid(r.Details) || r.ExamOn == default || r.ExamOn > Today || r.IssuedOn > Today || r.IssuedOn < r.ExamOn || r.NextEvaluationOn < r.ExamOn
            || r.CommandId == Guid.Empty || id.HasValue && (r.ExpectedRevision is not > 0 || r.ChangeCode is not ("REGISTRATION_ERROR" or "SOURCE_CORRECTION")))
            return Failure(400, "VALIDATION_FAILED", "Confira os dados do ASO emitido, datas, motivo e confirmação.");
        var unit = await access.UnitAsync(r.UnitId, "aso", "editar", ct); var scope = await access.ReadAsync(ct);
        if (!Editable(scope, unit.CompanyId, unit.Id)) throw new ResourceForbiddenException();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        OccupationalCertificate item; string? before = null;
        if (id.HasValue) {
            item = await db.OccupationalCertificates.SingleOrDefaultAsync(a => a.Id == id && a.UnitId == unit.Id && a.CompanyId == unit.CompanyId && a.Active && a.DeletedAt == null, ct)
                ?? throw new SESMT.Application.Exceptions.NotFoundException("ASO não encontrado.");
            if (!await (await Query(unit.Id, ct)).AnyAsync(a => a.Id == item.Id, ct)) return Failure(404, "NOT_FOUND", "ASO não encontrado.");
            if (item.Revision != r.ExpectedRevision || item.RevokedAt.HasValue) return Changed();
            if (item.WorkerId != r.WorkerId) return Failure(400, "VALIDATION_FAILED", "O trabalhador não pode ser substituído.");
            before = Snapshot(item);
        } else {
            var worker = await access.WorkerAsync(r.WorkerId, "ler", ct);
            if (worker.CompanyId != unit.CompanyId || worker.UnitId != unit.Id) return Failure(422, "INVALID_WORKER", "Trabalhador fora da unidade.");
            item = new(unit.CompanyId, unit.Id, worker.Id, r.CommandId, Actor); db.Add(item);
        }
        var details = r.Details with { EvaluatedFunction = r.Details.EvaluatedFunction.Trim(), PhysicianName = r.Details.PhysicianName.Trim(), Reference = r.Details.Reference?.Trim() };
        item.Set(r.ExamKind, r.ExamOn, r.IssuedOn, r.NextEvaluationOn, protector.Protect(JsonSerializer.Serialize(details)));
        if (id.HasValue) item.RecordCorrection(Actor);
        await Audit(id.HasValue ? "ASO_CORRECTED" : "ASO_REGISTERED", item, before, id.HasValue ? r.ChangeCode : null, ct);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Changed(); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return Failure(409, "DUPLICATE_COMMAND", "Esta solicitação já foi registrada. Atualize a consulta."); }
        return StatusCode(id.HasValue ? 200 : 201, await View(item, await Latest(await Query(unit.Id, ct)).ContainsAsync(item.Id, ct), ct));
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, RevokeAsoRequest r, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!r.Confirmation || r.ExpectedRevision < 1 || r.ReasonCode is not ("ENTRY_ERROR" or "DUPLICATE" or "REPLACED")) return Failure(400, "VALIDATION_FAILED", "Confirme o motivo administrativo e a revisão.");
        var unit = await access.UnitAsync(r.UnitId, "aso", "editar", ct); var s = await access.ReadAsync(ct);
        if (!Editable(s, unit.CompanyId, unit.Id)) throw new ResourceForbiddenException();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await (await Query(unit.Id, ct)).AnyAsync(a => a.Id == id, ct)) return Failure(404, "NOT_FOUND", "ASO não encontrado.");
        var item = await db.OccupationalCertificates.SingleAsync(a => a.Id == id, ct);
        if (item.Revision != r.ExpectedRevision || item.RevokedAt.HasValue) return Changed();
        if (await View(item, false, ct) == null) return Unavailable();
        var before = Snapshot(item); item.Revoke(r.ReasonCode, Actor); await Audit("ASO_REVOKED", item, before, r.ReasonCode, ct);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (DbUpdateConcurrencyException) { return Changed(); }
        // Return the persisted timestamp precision, also used by the verification GET.
        var stored = await (await Query(unit.Id, ct)).SingleAsync(a => a.Id == id, ct);
        return Ok(await View(stored, false, ct));
    }

    private static bool Valid(AsoDetails? d) => d != null && (d.Fitness is "FIT" or "UNFIT") && !string.IsNullOrWhiteSpace(d.EvaluatedFunction) && d.EvaluatedFunction.Trim().Length <= 160
        && !string.IsNullOrWhiteSpace(d.PhysicianName) && d.PhysicianName.Trim().Length <= 160 && d.PhysicianCrm is { Length: >= 1 and <= 12 }
        && d.PhysicianCrm.All(char.IsAsciiDigit) && States.Contains(d.PhysicianState) && (d.Reference?.Length ?? 0) <= 80;
    private async Task<object?> View(OccupationalCertificate a, bool isLatest, CancellationToken ct)
    {
        AsoDetails? d;
        try { d = JsonSerializer.Deserialize<AsoDetails>(protector.Unprotect(a.ProtectedDetails)); }
        catch (Exception e) when (e is CryptographicException or JsonException) { return null; }
        if (!Valid(d)) return null;
        var s = await access.ReadAsync(ct); var full = s.Can("aso:detalhar", a.CompanyId) || s.OwnOnly && s.WorkerId == a.WorkerId;
        return new { a.Id, a.CompanyId, a.UnitId, a.WorkerId, workerName = await db.Workers.Where(w => w.Id == a.WorkerId).Select(w => w.Name.Value).SingleAsync(ct),
            a.ExamKind, a.ExamOn, a.IssuedOn, a.NextEvaluationOn, d!.Fitness, d.EvaluatedFunction,
            medical = full ? new { d.PhysicianName, d.PhysicianCrm, d.PhysicianState, d.Reference } : null,
            a.Revision, a.RevokedAt, a.RevocationCode, isLatest };
    }
    private IActionResult Unavailable() => Failure(503, "ASO_UNAVAILABLE", "Não foi possível ler os dados protegidos. Contate o responsável pelo sistema.");
    private IActionResult Changed() => Failure(409, "ASO_CHANGED", "Este registro foi alterado ou invalidado. Atualize a consulta.");
    private string Snapshot(OccupationalCertificate a) => protector.Protect(JsonSerializer.Serialize(new { a.Id, a.WorkerId, a.ExamKind, a.ExamOn, a.IssuedOn, a.NextEvaluationOn, a.ProtectedDetails, a.Revision, a.RevokedAt, a.RevocationCode }));
    private async Task Audit(string action, OccupationalCertificate a, string? before, string? reason, CancellationToken ct)
    {
        async Task<string> Canonical(object value) => await db.Database.SqlQuery<string>($"SELECT CAST(CAST({JsonSerializer.Serialize(value)} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
        db.Add(new AuditLog(action, "aso_registro", a.Id, Actor, actor.Email, actor.Name, before == null ? null : await Canonical(new { protectedSnapshot = before }),
            await Canonical(new { protectedSnapshot = Snapshot(a), reasonCode = reason }), null, actor.IpAddress, actor.UserAgent,
            await db.AuditLogs.OrderByDescending(x => x.Id).Select(x => x.CurrentHash).FirstOrDefaultAsync(ct), companyId: a.CompanyId, resourceVersion: a.Revision,
            result: "SUCCESS", traceId: actor.TraceId));
    }
}
