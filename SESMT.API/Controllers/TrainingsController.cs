using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Infrastructure.Persistence;
using System.Text.Json;

namespace SESMT.API.Controllers;

public sealed record TrainingRequest(Guid UnitId, Guid WorkerId, Guid CommandId, string Title, string? Reference, string Kind,
    DateOnly CompletedOn, DateOnly? RenewalOn, int DurationMinutes, string? Provider, string? Instructor, string? Venue,
    string? Content, string? Notes, bool Confirmation, int? ExpectedRevision, string? ChangeReason);
public sealed record RevokeTrainingRequest(Guid UnitId, int ExpectedRevision, string Reason, bool Confirmation);
public sealed record TrainingView
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid UnitId { get; init; }
    public Guid WorkerId { get; init; }
    public string WorkerName { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Reference { get; init; }
    public string Kind { get; init; } = "";
    public DateOnly CompletedOn { get; init; }
    public DateOnly? RenewalOn { get; init; }
    public int DurationMinutes { get; init; }
    public string? Provider { get; init; }
    public string? Instructor { get; init; }
    public string? Venue { get; init; }
    public string? Content { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public string? RevocationReason { get; init; }
    public int Revision { get; init; }
}

[ApiController, Authorize, Route("trainings")]
public sealed class TrainingsController(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor) : ApiControllerBase
{
    private Guid Actor => actor.UserId ?? throw new UnauthorizedAccessException();
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    [HttpGet("context")]
    public async Task<IActionResult> Context(Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var unit = await access.UnitAsync(unitId, "treinamento", "ler", ct);
        var scope = await access.ReadAsync(ct);
        var canCreate = !scope.OwnOnly && scope.FullUnitIds.Contains(unitId)
            && scope.Can("treinamento:criar", unit.CompanyId) && scope.Can("trabalhador:ler", unit.CompanyId);
        var workers = canCreate ? await (await access.WorkersAsync("ler", ct))
            .Where(w => w.UnitId == unitId && w.Active && w.DismissalDate == null)
            .OrderBy(w => w.Name.Value).Select(w => new { w.Id, name = w.Name.Value, w.Enrollment }).Take(500).ToArrayAsync(ct) : [];
        return Ok(new { unitId, companyId = unit.CompanyId, ownOnly = scope.OwnOnly, canCreate,
            canEdit = !scope.OwnOnly && scope.FullUnitIds.Contains(unitId) && scope.Can("treinamento:editar", unit.CompanyId), workers });
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid unitId, CancellationToken ct, string? q = null, string? status = null,
        Guid? workerId = null, int page = 1, int pageSize = 20)
    {
        Response.Headers.CacheControl = "no-store";
        if (q?.Length > 160 || page is < 1 or > 1000000 || pageSize is < 1 or > 100
            || status is not (null or "" or "CURRENT" or "EXPIRING" or "EXPIRED" or "NO_DATE" or "REVOKED"))
            return Failure(400, "VALIDATION_FAILED", "Busca, situação ou paginação inválida.");
        var query = await Query(unitId, ct); var today = Today; var soon = today.AddDays(30);
        if (!string.IsNullOrWhiteSpace(q)) { var text = q.Trim().ToLower(); query = query.Where(t => t.Title.ToLower().Contains(text)
            || t.WorkerName.ToLower().Contains(text) || t.Reference != null && t.Reference.ToLower().Contains(text)); }
        if (workerId.HasValue) query = query.Where(t => t.WorkerId == workerId);
        if (status == "REVOKED") query = query.Where(t => t.RevokedAt != null);
        if (status is "CURRENT" or "EXPIRING" or "EXPIRED" or "NO_DATE") query = query.Where(t => t.RevokedAt == null);
        if (status == "CURRENT") query = query.Where(t => t.RenewalOn > soon);
        if (status == "EXPIRING") query = query.Where(t => t.RenewalOn >= today && t.RenewalOn <= soon);
        if (status == "EXPIRED") query = query.Where(t => t.RenewalOn < today);
        if (status == "NO_DATE") query = query.Where(t => t.RenewalOn == null);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(t => t.RenewalOn == null).ThenBy(t => t.RenewalOn)
            .ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await (await Query(unitId, ct)).SingleOrDefaultAsync(t => t.Id == id, ct);
        return item is null ? Failure(404, "NOT_FOUND", "Treinamento não encontrado.") : Ok(item);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await (await Query(unitId, ct)).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (item is null) return Failure(404, "NOT_FOUND", "Treinamento não encontrado.");
        var events = await db.AuditLogs.AsNoTracking().Where(a => a.ResourceType == "treinamento" && a.ResourceId == id && a.CompanyId == item.CompanyId)
            .OrderByDescending(a => a.Id).Take(100).Select(a => new { a.EventId, a.Action, a.ResourceVersion, a.ActorName, a.OccurredAt, a.NewData }).ToArrayAsync(ct);
        return Ok(events.Select(a => new { id = a.EventId, a.Action, revision = a.ResourceVersion, a.ActorName, a.OccurredAt,
            reason = ReadReason(a.NewData) }));
    }

    [HttpPost]
    public Task<IActionResult> Create(TrainingRequest request, CancellationToken ct) => Save(null, request, ct);
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Edit(Guid id, TrainingRequest request, CancellationToken ct) => Save(id, request, ct);

    private async Task<IActionResult> Save(Guid? id, TrainingRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.CompletedOn == default || request.CompletedOn > Today
            || id.HasValue && (request.ExpectedRevision is not > 0 || request.ChangeReason?.Trim().Length is not (>= 3 and <= 200)))
            return Failure(400, "VALIDATION_FAILED", "Confira a data, confirmação, revisão e motivo da alteração.");
        var unit = await access.UnitAsync(request.UnitId, "treinamento", id.HasValue ? "editar" : "criar", ct);
        await access.RequireAsync("treinamento:ler", unit.CompanyId, false, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        TrainingRecord item; TrainingView? before = null;
        if (id.HasValue)
        {
            item = await db.TrainingRecords.SingleOrDefaultAsync(t => t.Id == id && t.UnitId == unit.Id && t.CompanyId == unit.CompanyId
                && t.Active && t.DeletedAt == null, ct) ?? throw new SESMT.Application.Exceptions.NotFoundException("Treinamento não encontrado.");
            if (item.Revision != request.ExpectedRevision || item.RevokedAt.HasValue)
                return Failure(409, "TRAINING_CHANGED", "Este registro foi alterado ou invalidado. Atualize a consulta.");
            if (request.WorkerId != item.WorkerId) return Failure(400, "VALIDATION_FAILED", "O trabalhador não pode ser substituído. Invalide o registro incorreto.");
            before = await (await Query(unit.Id, ct)).SingleAsync(t => t.Id == item.Id, ct);
        }
        else
        {
            var worker = await access.WorkerAsync(request.WorkerId, "ler", ct);
            if (worker.CompanyId != unit.CompanyId || worker.UnitId != unit.Id || !worker.Active || worker.DismissalDate.HasValue)
                return Failure(422, "INVALID_WORKER", "Selecione um trabalhador ativo nesta unidade.");
            item = new TrainingRecord(unit.CompanyId, unit.Id, worker.Id, request.CommandId, Actor); db.Add(item);
        }
        item.Set(request.Title, request.Reference, request.Kind, request.CompletedOn, request.RenewalOn, request.DurationMinutes,
            request.Provider, request.Instructor, request.Venue, request.Content, request.Notes);
        if (id.HasValue) item.RecordUpdate(Actor);
        await Audit(id.HasValue ? "TRAINING_UPDATED" : "TRAINING_CREATED", item, before, id.HasValue ? request.ChangeReason : null, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Failure(409, "TRAINING_CHANGED", "O registro foi alterado durante a gravação."); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return Failure(409, "DUPLICATE_COMMAND", "Esta solicitação já foi registrada. Consulte os treinamentos antes de repetir."); }
        return StatusCode(id.HasValue ? 200 : 201, await (await Query(unit.Id, ct)).SingleAsync(t => t.Id == item.Id, ct));
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, RevokeTrainingRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.ExpectedRevision < 1) return Failure(400, "VALIDATION_FAILED", "Confirme a revisão e a invalidação.");
        var unit = await access.UnitAsync(request.UnitId, "treinamento", "editar", ct);
        await access.RequireAsync("treinamento:ler", unit.CompanyId, false, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.TrainingRecords.SingleOrDefaultAsync(t => t.Id == id && t.UnitId == unit.Id && t.CompanyId == unit.CompanyId
            && t.Active && t.DeletedAt == null, ct);
        if (item is null) return Failure(404, "NOT_FOUND", "Treinamento não encontrado.");
        if (item.Revision != request.ExpectedRevision || item.RevokedAt.HasValue) return Failure(409, "TRAINING_CHANGED", "Atualize a consulta antes de invalidar este registro.");
        var before = await (await Query(unit.Id, ct)).SingleAsync(t => t.Id == id, ct);
        item.Revoke(request.Reason, Actor, DateTimeOffset.UtcNow); await Audit("TRAINING_REVOKED", item, before, request.Reason, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Failure(409, "TRAINING_CHANGED", "O registro foi alterado durante a gravação."); }
        return Ok(await (await Query(unit.Id, ct)).SingleAsync(t => t.Id == id, ct));
    }

    private async Task<IQueryable<TrainingView>> Query(Guid unitId, CancellationToken ct)
    {
        var unit = await access.UnitAsync(unitId, "treinamento", "ler", ct); var scope = await access.ReadAsync(ct); var sectors = scope.SectorIds.ToArray();
        return from t in db.TrainingRecords.AsNoTracking() join w in db.Workers.AsNoTracking() on t.WorkerId equals w.Id
            where t.UnitId == unit.Id && t.CompanyId == unit.CompanyId && t.Active && t.DeletedAt == null
                && (!scope.OwnOnly || t.WorkerId == scope.WorkerId)
                && (scope.FullUnitIds.Contains(unit.Id) || w.DepartmentId.HasValue && sectors.Contains(w.DepartmentId.Value))
            select new TrainingView { Id=t.Id, CompanyId=t.CompanyId, UnitId=t.UnitId, WorkerId=t.WorkerId, WorkerName=w.Name.Value,
                Title=t.Title, Reference=t.Reference, Kind=t.Kind, CompletedOn=t.CompletedOn, RenewalOn=t.RenewalOn, DurationMinutes=t.DurationMinutes,
                Provider=t.Provider, Instructor=t.Instructor, Venue=t.Venue, Content=t.Content, Notes=t.Notes,
                RevokedAt=t.RevokedAt, RevocationReason=t.RevocationReason, Revision=t.Revision };
    }
    private static string? ReadReason(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null;
    }
    private async Task Audit(string action, TrainingRecord item, TrainingView? before, string? reason, CancellationToken ct)
    {
        async Task<string> Canonical(object value) => await db.Database.SqlQuery<string>($"SELECT CAST(CAST({JsonSerializer.Serialize(value)} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
        var previous = before is null ? null : await Canonical(before);
        var data = await Canonical(new { item.Title, item.Reference, item.Kind, item.WorkerId, item.CompletedOn, item.RenewalOn, item.DurationMinutes,
            item.Provider, item.Instructor, item.Venue, item.Content, item.Notes, item.RevokedAt, item.RevocationReason, reason = reason?.Trim() });
        db.Add(new AuditLog(action, "treinamento", item.Id, Actor, actor.Email, actor.Name, previous, data, null, actor.IpAddress, actor.UserAgent,
            await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct), companyId:item.CompanyId,
            resourceVersion:item.Revision, result:"SUCCESS", traceId:actor.TraceId));
    }
}
