using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Equipment.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

public sealed record EquipmentRequest(Guid UnitId, string Code, string Name, string? ApprovalCertificate, string? Manufacturer, string? Model, bool Confirmation, int? ExpectedRevision);
public sealed record EquipmentDeliveryRequest(Guid UnitId, Guid WorkerId, Guid EquipmentId, Guid CommandId, int Quantity, string? Size, DateOnly DeliveredOn, DateOnly? ReplacementOn, string? Notes, bool Confirmation);
public sealed record EquipmentReturnRequest(Guid UnitId, int ExpectedRevision, DateOnly ReturnedOn, string Reason, bool Confirmation);
public sealed record EquipmentView(Guid Id, Guid CompanyId, string Code, string Name, string? ApprovalCertificate, string? Manufacturer, string? Model, int Revision);
public sealed record EquipmentDeliveryView
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid UnitId { get; init; }
    public Guid WorkerId { get; init; }
    public string WorkerName { get; init; } = "";
    public Guid EquipmentId { get; init; }
    public string EquipmentCode { get; init; } = "";
    public string EquipmentName { get; init; } = "";
    public string? ApprovalCertificate { get; init; }
    public int Quantity { get; init; }
    public string? Size { get; init; }
    public DateOnly DeliveredOn { get; init; }
    public DateOnly? ReplacementOn { get; init; }
    public string? Notes { get; init; }
    public DateOnly? ReturnedOn { get; init; }
    public string? ReturnReason { get; init; }
    public int Revision { get; init; }
}

[ApiController, Authorize, Route("equipment")]
public sealed class EquipmentController(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor) : ApiControllerBase
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    private Guid Actor => actor.UserId ?? throw new UnauthorizedAccessException();
    [HttpGet("context")]
    public async Task<IActionResult> Context(Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var unit = await access.UnitAsync(unitId, "epi", "ler", ct); var scope = await access.ReadAsync(ct);
        var canDeliver = !scope.OwnOnly && scope.Can("epi:criar", unit.CompanyId) && scope.Can("trabalhador:ler", unit.CompanyId) && scope.FullUnitIds.Contains(unitId);
        var workers = canDeliver ? await (await access.WorkersAsync("ler", ct)).Where(w => w.UnitId == unitId && w.Active && w.DismissalDate == null)
            .OrderBy(w => w.Name.Value).Select(w => new { w.Id, name = w.Name.Value, w.Enrollment }).Take(500).ToArrayAsync(ct) : [];
        var equipment = await db.Equipment.Where(e => e.CompanyId == unit.CompanyId && e.Active && e.DeletedAt == null)
            .OrderBy(e => e.Name).Select(e => new { e.Id, e.Name, e.Code }).Take(500).ToArrayAsync(ct);
        return Ok(new { unitId, companyId = unit.CompanyId, unitName = unit.Name, ownOnly = scope.OwnOnly,
            canCreate = !scope.OwnOnly && scope.IsFullCompany(unit.CompanyId) && scope.Can("epi:criar", unit.CompanyId),
            canEdit = !scope.OwnOnly && scope.IsFullCompany(unit.CompanyId) && scope.Can("epi:editar", unit.CompanyId),
            canDeliver, canReturn = !scope.OwnOnly && scope.Can("epi:editar", unit.CompanyId) && scope.FullUnitIds.Contains(unitId), workers, equipment });
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid unitId, CancellationToken ct, string? q = null, int page = 1, int pageSize = 20)
    {
        Response.Headers.CacheControl = "no-store"; ValidatePage(q, page, pageSize);
        var unit = await access.UnitAsync(unitId, "epi", "ler", ct);
        var query = db.Equipment.AsNoTracking().Where(e => e.CompanyId == unit.CompanyId && e.Active && e.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(q)) { var text = q.Trim().ToLower(); query = query.Where(e => e.Name.ToLower().Contains(text) || e.Code.ToLower().Contains(text) || e.ApprovalCertificate != null && e.ApprovalCertificate.ToLower().Contains(text)); }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(e => e.Name).ThenBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EquipmentView(e.Id, e.CompanyId, e.Code, e.Name, e.ApprovalCertificate, e.Manufacturer, e.Model, e.Revision)).ToArrayAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
    [HttpPost]
    public async Task<IActionResult> Create(EquipmentRequest request, CancellationToken ct) => await SaveCatalog(null, request, ct);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; var unit = await access.UnitAsync(unitId, "epi", "ler", ct);
        var item = await db.Equipment.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.CompanyId == unit.CompanyId && e.Active && e.DeletedAt == null, ct);
        return item is null ? Failure(404, "NOT_FOUND", "Equipamento não encontrado.") : Ok(View(item));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, EquipmentRequest request, CancellationToken ct) => await SaveCatalog(id, request, ct);
    private async Task<IActionResult> SaveCatalog(Guid? id, EquipmentRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || id.HasValue && request.ExpectedRevision is not > 0) return Failure(400, "VALIDATION_FAILED", "Confirme o cadastro e sua revisão.");
        var unit = await access.UnitAsync(request.UnitId, "epi", "ler", ct);
        var scope = await access.RequireAsync(id.HasValue ? "epi:editar" : "epi:criar", unit.CompanyId, true, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        ProtectiveEquipment item;
        if (id.HasValue)
        {
            item = await db.Equipment.SingleOrDefaultAsync(e => e.Id == id && e.CompanyId == unit.CompanyId && e.Active && e.DeletedAt == null, ct)
                ?? throw new SESMT.Application.Exceptions.NotFoundException("Equipamento não encontrado.");
            if (item.Revision != request.ExpectedRevision) return Failure(409, "EQUIPMENT_CHANGED", "O equipamento foi alterado. Atualize o cadastro.");
            item.Update(request.Code, request.Name, request.ApprovalCertificate, request.Manufacturer, request.Model, Actor);
        }
        else { item = new(unit.CompanyId, request.Code, request.Name, request.ApprovalCertificate, request.Manufacturer, request.Model, Actor); db.Add(item); }
        var view = View(item); await Audit(id.HasValue ? "EQUIPMENT_UPDATED" : "EQUIPMENT_CREATED", item.Id, unit.CompanyId, item.Revision, view, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Failure(409, "EQUIPMENT_CHANGED", "O equipamento foi alterado durante a gravação."); }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return Failure(409, "DUPLICATE_CODE", "Este código de equipamento já está cadastrado na empresa."); }
        return StatusCode(id.HasValue ? 200 : 201, view);
    }
    [HttpGet("deliveries")]
    public async Task<IActionResult> Deliveries(Guid unitId, CancellationToken ct, string? q = null, Guid? workerId = null, string? status = null, int page = 1, int pageSize = 20)
    {
        Response.Headers.CacheControl = "no-store"; ValidatePage(q, page, pageSize);
        if (status is not (null or "" or "ACTIVE" or "RETURNED" or "DUE")) throw new ArgumentException("Situação inválida.");
        var query = await DeliveryQuery(unitId, ct);
        if (workerId.HasValue) query = query.Where(d => d.WorkerId == workerId);
        if (status == "ACTIVE") query = query.Where(d => d.ReturnedOn == null);
        if (status == "RETURNED") query = query.Where(d => d.ReturnedOn != null);
        if (status == "DUE") { var today = Today; query = query.Where(d => d.ReturnedOn == null && d.ReplacementOn != null && d.ReplacementOn <= today); }
        if (!string.IsNullOrWhiteSpace(q)) { var text = q.Trim().ToLower(); query = query.Where(d => d.WorkerName.ToLower().Contains(text) || d.EquipmentName.ToLower().Contains(text) || d.EquipmentCode.ToLower().Contains(text)); }
        var total = await query.CountAsync(ct); var items = await query.OrderByDescending(d => d.DeliveredOn).ThenBy(d => d.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
    [HttpGet("deliveries/{id:guid}")]
    public async Task<IActionResult> Delivery(Guid id, Guid unitId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var delivery = await (await DeliveryQuery(unitId, ct)).SingleOrDefaultAsync(d => d.Id == id, ct);
        return delivery is null ? Failure(404, "NOT_FOUND", "Entrega não encontrada.") : Ok(delivery);
    }
    [HttpPost("deliveries")]
    public async Task<IActionResult> Deliver(EquipmentDeliveryRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.DeliveredOn > Today || request.DeliveredOn == default) return Failure(400, "VALIDATION_FAILED", "Confirme uma data de entrega válida, até hoje.");
        var unit = await access.UnitAsync(request.UnitId, "epi", "criar", ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var worker = await access.WorkerAsync(request.WorkerId, "ler", ct);
        if (worker.CompanyId != unit.CompanyId || worker.UnitId != unit.Id || !worker.Active || worker.DismissalDate.HasValue || worker.AdmissionDate > request.DeliveredOn)
            return Failure(422, "INVALID_WORKER", "Selecione um trabalhador com vínculo ativo nesta unidade e na data da entrega.");
        var equipment = await db.Equipment.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.EquipmentId && e.CompanyId == unit.CompanyId && e.Active && e.DeletedAt == null, ct);
        if (equipment is null) return Failure(404, "NOT_FOUND", "Equipamento não encontrado na empresa.");
        var item = new EquipmentDelivery(equipment, unit.Id, worker.Id, request.CommandId, request.Quantity, request.Size, request.DeliveredOn, request.ReplacementOn, request.Notes, Actor);
        db.Add(item); await Audit("EQUIPMENT_DELIVERED", item.Id, unit.CompanyId, item.Revision, new { item.WorkerId, item.EquipmentId, item.Quantity, item.DeliveredOn, item.ReplacementOn }, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return Failure(409, "DUPLICATE_COMMAND", "Esta solicitação já foi registrada. Consulte as entregas antes de repetir."); }
        return StatusCode(201, await (await DeliveryQuery(unit.Id, ct)).SingleAsync(d => d.Id == item.Id, ct));
    }
    [HttpPost("deliveries/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, EquipmentReturnRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.ExpectedRevision < 1 || request.ReturnedOn > Today || request.ReturnedOn == default)
            return Failure(400, "VALIDATION_FAILED", "Confirme a devolução, data e revisão da entrega.");
        var unit = await access.UnitAsync(request.UnitId, "epi", "editar", ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.EquipmentDeliveries.SingleOrDefaultAsync(d => d.Id == id && d.UnitId == unit.Id && d.CompanyId == unit.CompanyId && d.Active && d.DeletedAt == null, ct);
        if (item is null) return Failure(404, "NOT_FOUND", "Entrega não encontrada.");
        if (item.Revision != request.ExpectedRevision || item.ReturnedOn.HasValue) return Failure(409, "DELIVERY_CHANGED", "Esta entrega já foi alterada. Atualize a consulta.");
        item.Return(request.ReturnedOn, request.Reason, Actor);
        await Audit("EQUIPMENT_RETURNED", item.Id, unit.CompanyId, item.Revision, new { item.WorkerId, item.ReturnedOn, item.ReturnReason }, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Failure(409, "DELIVERY_CHANGED", "A entrega foi alterada durante a gravação."); }
        return Ok(await (await DeliveryQuery(unit.Id, ct)).SingleAsync(d => d.Id == id, ct));
    }
    private async Task<IQueryable<EquipmentDeliveryView>> DeliveryQuery(Guid unitId, CancellationToken ct)
    {
        var unit = await access.UnitAsync(unitId, "epi", "ler", ct); var scope = await access.ReadAsync(ct);
        var sectors = scope.SectorIds.ToArray();
        return from d in db.EquipmentDeliveries.AsNoTracking() join w in db.Workers.AsNoTracking() on d.WorkerId equals w.Id
            where d.UnitId == unit.Id && d.CompanyId == unit.CompanyId && d.Active && d.DeletedAt == null
                && (!scope.OwnOnly || d.WorkerId == scope.WorkerId)
                && (scope.FullUnitIds.Contains(unit.Id) || w.DepartmentId.HasValue && sectors.Contains(w.DepartmentId.Value))
            select new EquipmentDeliveryView { Id=d.Id, CompanyId=d.CompanyId, UnitId=d.UnitId, WorkerId=d.WorkerId, WorkerName=w.Name.Value,
                EquipmentId=d.EquipmentId, EquipmentCode=d.EquipmentCode, EquipmentName=d.EquipmentName, ApprovalCertificate=d.ApprovalCertificate,
                Quantity=d.Quantity, Size=d.Size, DeliveredOn=d.DeliveredOn, ReplacementOn=d.ReplacementOn, Notes=d.Notes,
                ReturnedOn=d.ReturnedOn, ReturnReason=d.ReturnReason, Revision=d.Revision };
    }
    private static EquipmentView View(ProtectiveEquipment e) => new(e.Id, e.CompanyId, e.Code, e.Name, e.ApprovalCertificate, e.Manufacturer, e.Model, e.Revision);
    private static void ValidatePage(string? q, int page, int pageSize)
    { if (q?.Length > 160 || page is < 1 or > 1000000 || pageSize is < 1 or > 100) throw new ArgumentException("Busca ou paginação inválida."); }
    private async Task Audit(string action, Guid id, Guid company, int revision, object data, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var stored = await db.Database.SqlQuery<string>($"SELECT CAST(CAST({json} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
        db.Add(new AuditLog(action, "epi", id, Actor, actor.Email, actor.Name, null, stored, null, actor.IpAddress, actor.UserAgent,
            await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct), companyId: company, resourceVersion: revision, result: "SUCCESS", traceId: actor.TraceId));
    }
}
