using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record TrainingRequest(Guid UnitId, Guid WorkerId, Guid CommandId, string Title, string? Reference, string Kind,
    DateOnly CompletedOn, DateOnly? RenewalOn, int DurationMinutes, string? Provider, string? Instructor, string? Venue,
    string? Content, string? Notes, bool Confirmation, int? ExpectedRevision, string? ChangeReason);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
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
public sealed record TrainingContext(Guid UnitId, Guid CompanyId, bool OwnOnly, bool CanCreate, bool CanEdit, IReadOnlyList<TrainingWorkerOption> Workers);
public sealed record TrainingWorkerOption(Guid Id, string Name, string Enrollment);
public sealed record TrainingPage(IReadOnlyList<TrainingView> Items, int Total, int Page, int PageSize);
public sealed record TrainingHistoryEntry(Guid Id, string Action, int? Revision, string? ActorName, DateTimeOffset OccurredAt, string? Reason);

/// <summary>A refusal the client can act on, with its stable error code. The controller turns it into the error response.</summary>
public sealed class TrainingRefusal(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

/// <summary>
/// Administrative training records: scope, validation, revision control and audit. Moved out of the controller so
/// the HTTP layer only translates requests and responses; the rules and responses are unchanged.
/// </summary>
public sealed class TrainingService(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor)
{
    public static readonly string[] Statuses = ["CURRENT", "EXPIRING", "EXPIRED", "NO_DATE", "REVOKED"];
    private Guid Actor => actor.UserId ?? throw new UnauthorizedAccessException();
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);

    public async Task<TrainingContext> ContextAsync(Guid unitId, CancellationToken ct)
    {
        var unit = await access.UnitAsync(unitId, "treinamento", "ler", ct);
        var scope = await access.ReadAsync(ct);
        var canCreate = !scope.OwnOnly && scope.FullUnitIds.Contains(unitId)
            && scope.Can("treinamento:criar", unit.CompanyId) && scope.Can("trabalhador:ler", unit.CompanyId);
        var workers = canCreate ? await (await access.WorkersAsync("ler", ct))
            .Where(w => w.UnitId == unitId && w.Active && w.DismissalDate == null)
            .OrderBy(w => w.Name.Value).Select(w => new TrainingWorkerOption(w.Id, w.Name.Value, w.Enrollment)).Take(500).ToArrayAsync(ct) : [];
        return new(unitId, unit.CompanyId, scope.OwnOnly, canCreate,
            !scope.OwnOnly && scope.FullUnitIds.Contains(unitId) && scope.Can("treinamento:editar", unit.CompanyId), workers);
    }

    public async Task<TrainingPage> ListAsync(Guid unitId, string? q, string? status, Guid? workerId, int page, int pageSize, CancellationToken ct)
    {
        if (q?.Length > 160 || page is < 1 or > 1000000 || pageSize is < 1 or > 100 || !string.IsNullOrEmpty(status) && !Statuses.Contains(status))
            throw new TrainingRefusal(400, "VALIDATION_FAILED", "Busca, situação ou paginação inválida.");
        var query = await QueryAsync(unitId, ct); var today = Today; var soon = today.AddDays(30);
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
        return new(items, total, page, pageSize);
    }

    public async Task<TrainingView> GetAsync(Guid id, Guid unitId, CancellationToken ct)
        => await (await QueryAsync(unitId, ct)).SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw NotFound();

    public async Task<IReadOnlyList<TrainingHistoryEntry>> HistoryAsync(Guid id, Guid unitId, CancellationToken ct)
    {
        var item = await GetAsync(id, unitId, ct);
        var events = await db.AuditLogs.AsNoTracking().Where(a => a.ResourceType == "treinamento" && a.ResourceId == id && a.CompanyId == item.CompanyId)
            .OrderByDescending(a => a.Id).Take(100).Select(a => new { a.EventId, a.Action, a.ResourceVersion, a.ActorName, a.OccurredAt, a.NewData }).ToArrayAsync(ct);
        return events.Select(a => new TrainingHistoryEntry(a.EventId, a.Action, a.ResourceVersion, a.ActorName, a.OccurredAt, ReadReason(a.NewData))).ToArray();
    }

    /// <summary>Creates (id null) or edits a record. Returns the stored view.</summary>
    public async Task<TrainingView> SaveAsync(Guid? id, TrainingRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.CompletedOn == default || request.CompletedOn > Today
            || id.HasValue && (request.ExpectedRevision is not > 0 || request.ChangeReason?.Trim().Length is not (>= 3 and <= 200)))
            throw new TrainingRefusal(400, "VALIDATION_FAILED", "Confira a data, confirmação, revisão e motivo da alteração.");
        var unit = await access.UnitAsync(request.UnitId, "treinamento", id.HasValue ? "editar" : "criar", ct);
        await access.RequireAsync("treinamento:ler", unit.CompanyId, false, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        TrainingRecord item; TrainingView? before = null;
        if (id.HasValue)
        {
            item = await db.TrainingRecords.SingleOrDefaultAsync(t => t.Id == id && t.UnitId == unit.Id && t.CompanyId == unit.CompanyId
                && t.Active && t.DeletedAt == null, ct) ?? throw new NotFoundException("Treinamento não encontrado.");
            if (item.Revision != request.ExpectedRevision || item.RevokedAt.HasValue)
                throw new TrainingRefusal(409, "TRAINING_CHANGED", "Este registro foi alterado ou invalidado. Atualize a consulta.");
            if (request.WorkerId != item.WorkerId)
                throw new TrainingRefusal(400, "VALIDATION_FAILED", "O trabalhador não pode ser substituído. Invalide o registro incorreto.");
            before = await (await QueryAsync(unit.Id, ct)).SingleAsync(t => t.Id == item.Id, ct);
        }
        else
        {
            var worker = await access.WorkerAsync(request.WorkerId, "ler", ct);
            if (worker.CompanyId != unit.CompanyId || worker.UnitId != unit.Id || !worker.Active || worker.DismissalDate.HasValue)
                throw new TrainingRefusal(422, "INVALID_WORKER", "Selecione um trabalhador ativo nesta unidade.");
            if (await ReplayAsync(unit.CompanyId, request, ct) is { } replayed) return replayed;
            item = new TrainingRecord(unit.CompanyId, unit.Id, worker.Id, request.CommandId, Actor); db.Add(item);
        }
        item.Set(request.Title, request.Reference, request.Kind, request.CompletedOn, request.RenewalOn, request.DurationMinutes,
            request.Provider, request.Instructor, request.Venue, request.Content, request.Notes);
        if (id.HasValue) item.RecordUpdate(Actor);
        await AuditAsync(id.HasValue ? "TRAINING_UPDATED" : "TRAINING_CREATED", item, before, id.HasValue ? request.ChangeReason : null, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new TrainingRefusal(409, "TRAINING_CHANGED", "O registro foi alterado durante a gravação."); }
        catch (DbUpdateException e) when (!id.HasValue && e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // A parallel request with the same command won the insert: answer with its record when it is the same request.
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await ReplayAsync(unit.CompanyId, request, ct) ?? throw DuplicateCommand();
        }
        return await (await QueryAsync(unit.Id, ct)).SingleAsync(t => t.Id == item.Id, ct);
    }

    private static TrainingRefusal DuplicateCommand() =>
        new(409, "DUPLICATE_COMMAND", "Esta solicitação já foi registrada com outros dados. Consulte os treinamentos antes de repetir.");

    /// <summary>Retrying a create after a lost response must not fail or duplicate: the same person repeating the same
    /// command with the same data gets the record that command created. Any difference keeps the conflict.</summary>
    private async Task<TrainingView?> ReplayAsync(Guid companyId, TrainingRequest request, CancellationToken ct)
    {
        var existing = await db.TrainingRecords.AsNoTracking()
            .SingleOrDefaultAsync(t => t.CompanyId == companyId && t.CommandId == request.CommandId, ct);
        if (existing is null) return null;
        var expected = new TrainingRecord(companyId, request.UnitId, request.WorkerId, request.CommandId, Actor);
        expected.Set(request.Title, request.Reference, request.Kind, request.CompletedOn, request.RenewalOn, request.DurationMinutes,
            request.Provider, request.Instructor, request.Venue, request.Content, request.Notes);
        var same = existing.CreatedBy == Actor && existing.UnitId == request.UnitId && existing.WorkerId == request.WorkerId
            && existing.Active && existing.DeletedAt == null && existing.Revision == 1 && !existing.RevokedAt.HasValue
            && existing.Title == expected.Title && existing.Reference == expected.Reference && existing.Kind == expected.Kind
            && existing.CompletedOn == expected.CompletedOn && existing.RenewalOn == expected.RenewalOn
            && existing.DurationMinutes == expected.DurationMinutes && existing.Provider == expected.Provider
            && existing.Instructor == expected.Instructor && existing.Venue == expected.Venue
            && existing.Content == expected.Content && existing.Notes == expected.Notes;
        if (!same) throw DuplicateCommand();
        return await (await QueryAsync(existing.UnitId, ct)).SingleAsync(t => t.Id == existing.Id, ct);
    }

    public async Task<TrainingView> RevokeAsync(Guid id, RevokeTrainingRequest request, CancellationToken ct)
    {
        if (!request.Confirmation || request.ExpectedRevision < 1) throw new TrainingRefusal(400, "VALIDATION_FAILED", "Confirme a revisão e a invalidação.");
        var unit = await access.UnitAsync(request.UnitId, "treinamento", "editar", ct);
        await access.RequireAsync("treinamento:ler", unit.CompanyId, false, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.TrainingRecords.SingleOrDefaultAsync(t => t.Id == id && t.UnitId == unit.Id && t.CompanyId == unit.CompanyId
            && t.Active && t.DeletedAt == null, ct) ?? throw NotFound();
        if (item.Revision != request.ExpectedRevision || item.RevokedAt.HasValue)
            throw new TrainingRefusal(409, "TRAINING_CHANGED", "Atualize a consulta antes de invalidar este registro.");
        var before = await (await QueryAsync(unit.Id, ct)).SingleAsync(t => t.Id == id, ct);
        item.Revoke(request.Reason, Actor, DateTimeOffset.UtcNow); await AuditAsync("TRAINING_REVOKED", item, before, request.Reason, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new TrainingRefusal(409, "TRAINING_CHANGED", "O registro foi alterado durante a gravação."); }
        return await (await QueryAsync(unit.Id, ct)).SingleAsync(t => t.Id == id, ct);
    }

    private static TrainingRefusal NotFound() => new(404, "NOT_FOUND", "Treinamento não encontrado.");

    private async Task<IQueryable<TrainingView>> QueryAsync(Guid unitId, CancellationToken ct)
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

    private async Task AuditAsync(string action, TrainingRecord item, TrainingView? before, string? reason, CancellationToken ct)
    {
        async Task<string> Canonical(object value) => await db.Database.SqlQuery<string>($"SELECT CAST(CAST({JsonSerializer.Serialize(value)} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
        var previous = before is null ? null : await Canonical(before);
        var data = await Canonical(new { item.Title, item.Reference, item.Kind, item.WorkerId, item.CompletedOn, item.RenewalOn, item.DurationMinutes,
            item.Provider, item.Instructor, item.Venue, item.Content, item.Notes, item.RevokedAt, item.RevocationReason, reason = reason?.Trim() });
        db.Add(new AuditLog(action, "treinamento", item.Id, Actor, actor.Email, actor.Name, previous, data, null, actor.IpAddress, actor.UserAgent,
            await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct), companyId: item.CompanyId,
            resourceVersion: item.Revision, result: "SUCCESS", traceId: actor.TraceId));
    }
}
