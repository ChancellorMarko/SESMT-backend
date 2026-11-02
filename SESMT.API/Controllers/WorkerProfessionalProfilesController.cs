using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

public sealed record WorkerProfessionalRequest(string Name, string Enrollment, Guid? DepartmentId, Guid? PositionId, string Reason, int ExpectedRevision, bool Confirmation);

[ApiController, Authorize, Route("workers/{id:guid}/professional-profile")]
public sealed class WorkerProfessionalProfilesController(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var worker = await Editable(id, ct);
        if (worker is null) return Failure(404, "NOT_FOUND", "Trabalhador não encontrado ou fora do seu acesso.");
        if (!worker.Active || worker.DismissalDate.HasValue || !worker.UnitId.HasValue) return Failure(409, "WORKER_UNAVAILABLE", "Este vínculo não pode ser editado neste formulário.");
        var snapshot = await access.ReadAsync(ct);
        var sectors = await db.Departments.Where(d => d.UnitId == worker.UnitId && d.Active && d.DeletedAt == null && snapshot.SectorIds.Contains(d.Id))
            .OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToArrayAsync(ct);
        var positions = await db.Positions.Where(p => p.CompanyId == worker.CompanyId && p.Active && p.DeletedAt == null)
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToArrayAsync(ct);
        return Ok(new { worker = Profile(worker), unitName = await db.Units.Where(u => u.Id == worker.UnitId).Select(u => u.Name).SingleAsync(ct),
            requiresDepartment = !snapshot.FullUnitIds.Contains(worker.UnitId.Value), departments = sectors, positions });
    }

    [HttpPatch]
    public async Task<IActionResult> Update(Guid id, WorkerProfessionalRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.ExpectedRevision < 1 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200
            || string.IsNullOrWhiteSpace(request.Enrollment) || request.Enrollment.Trim().Length > 30 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 200)
            return Failure(400, "VALIDATION_FAILED", "Confira nome, matrícula, motivo, confirmação e revisão do cadastro.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var worker = await Editable(id, ct);
        if (worker is null) return Failure(404, "NOT_FOUND", "Trabalhador não encontrado ou fora do seu acesso.");
        if (!worker.Active || worker.DismissalDate.HasValue || !worker.UnitId.HasValue) return Failure(409, "WORKER_UNAVAILABLE", "Este vínculo não pode ser editado neste formulário.");
        if (worker.Revision != request.ExpectedRevision) return Failure(409, "WORKER_CHANGED", "O cadastro foi alterado. Consulte a revisão atual.");
        await access.ValidateWorkerContextAsync("editar", worker.CompanyId, worker.UnitId, request.DepartmentId, request.PositionId, ct);
        if (request.DepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == request.DepartmentId && d.UnitId == worker.UnitId && d.Active && d.DeletedAt == null, ct))
            return Failure(422, "INVALID_DEPARTMENT", "O setor não pertence à unidade do trabalhador.");
        if (await db.Workers.AnyAsync(w => w.CompanyId == worker.CompanyId && w.Id != id && w.Enrollment == request.Enrollment.Trim() && w.DeletedAt == null, ct))
            return Failure(409, "DUPLICATE_ENROLLMENT", "Já existe um trabalhador com esta matrícula na empresa.");
        var assignmentChanged = worker.DepartmentId != request.DepartmentId || worker.PositionId != request.PositionId;
        if (assignmentChanged && (!request.DepartmentId.HasValue || !request.PositionId.HasValue))
            return Failure(422, "INCOMPLETE_ASSIGNMENT", "Selecione cargo e setor para registrar a mudança profissional.");
        if (assignmentChanged)
        {
            var histories = await db.Set<WorkerHistory>().Where(h => h.WorkerId == id && h.EndDate == null && h.Active && h.DeletedAt == null).ToArrayAsync(ct);
            if (histories.Length > 1) return Failure(409, "HISTORY_CONFLICT", "Há mais de um vínculo profissional em aberto. Revise o histórico antes de alterar.");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            if (histories.Any(h => h.StartDate > today)) return Failure(409, "HISTORY_CONFLICT", "O histórico possui uma data futura e precisa de revisão.");
            foreach (var history in histories) history.Close(today, request.Reason.Trim());
            db.Add(new WorkerHistory(id, request.DepartmentId!.Value, request.PositionId!.Value, today, reasonForChange: request.Reason.Trim()));
        }
        var previous = await StorageJson(Profile(worker), ct);
        worker.Update(request.Enrollment, request.Name, worker.DateOfBirth, worker.AdmissionDate, worker.DismissalDate, request.PositionId, worker.UnitId, request.DepartmentId);
        var current = await StorageJson(Profile(worker), ct);
        var context = await StorageJson(new { reason = request.Reason.Trim() }, ct);
        db.Add(new AuditLog("PROFESSIONAL_PROFILE_UPDATED", "trabalhador", id, actor.UserId, actor.Email, actor.Name, previous, current, context,
            actor.IpAddress, actor.UserAgent, await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct),
            companyId: worker.CompanyId, resourceVersion: worker.Revision, result: "SUCCESS", traceId: actor.TraceId));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Failure(409, "WORKER_CHANGED", "O cadastro foi alterado durante a gravação. Consulte a revisão atual."); }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return Failure(409, "DUPLICATE_ENROLLMENT", "Já existe um cadastro com estes identificadores."); }
        return Ok(Profile(worker));
    }
    private async Task<Worker?> Editable(Guid id, CancellationToken ct)
    {
        var worker = await (await access.WorkersAsync("editar", ct)).SingleOrDefaultAsync(w => w.Id == id, ct);
        if (worker is not null) await access.RequireAsync("trabalhador:ler", worker.CompanyId, false, ct);
        return worker;
    }
    private static object Profile(Worker worker) => new { worker.Id, worker.CompanyId, worker.UnitId, worker.DepartmentId, worker.PositionId, worker.Enrollment, name = worker.Name.Value, worker.Revision };
    private Task<string> StorageJson(object value, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return db.Database.SqlQuery<string>($"SELECT CAST(CAST({json} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
    }
}
