using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record WorkerAccountContext(Guid WorkerId, Guid CompanyId, Guid? UnitId, string Name, string Enrollment, int Revision, bool Active, Guid? UserId);
public sealed record WorkerAccountCandidate(Guid Id, string Name, string Email);
public sealed record WorkerAccountCandidates(Guid WorkerId, Guid CompanyId, Guid? UnitId, int Revision, WorkerAccountCandidate[] Items, int Total, int Page, int PageSize);
public sealed class LinkWorkerAccountRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; set; }
    public Guid UserId { get; set; }
    [Required, StringLength(200)] public string ExpectedAccountName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string ExpectedAccountEmail { get; set; } = "";
    public bool Confirmation { get; set; }
}

/// <summary>Initial identity binding only. Replacement/unlinking require a separate historical policy.</summary>
public sealed class WorkerAccountBindingService(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor, IAuditLogRepository audit)
{
    private async Task<Worker> AuthorizeAsync(Guid id, CancellationToken ct)
    {
        var worker = await access.WorkerAsync(id, "ler", ct);
        await access.WorkerAsync(id, "editar", ct);
        await access.RequireAsync("usuario:ler", worker.CompanyId, true, ct);
        await access.RequireAsync("usuario:editar", worker.CompanyId, true, ct);
        return worker;
    }
    private static WorkerAccountContext Context(Worker worker) => new(worker.Id, worker.CompanyId, worker.UnitId, worker.Name.Value, worker.Enrollment, worker.Revision, worker.Active && worker.DeletedAt == null && worker.DismissalDate == null, worker.UserId);
    public async Task<WorkerAccountContext> ReadAsync(Guid id, CancellationToken ct) => Context(await AuthorizeAsync(id, ct));

    private IQueryable<User> Candidates(Worker worker)
        => db.Users.AsNoTracking().Where(u => u.Active && u.DeletedAt == null
            && !db.Workers.Any(w => w.UserId == u.Id && w.DeletedAt == null)
            && (db.UserRoles.Any(a => a.UserId == u.Id && a.CompanyId == worker.CompanyId && a.Active && a.DeletedAt == null
                    && db.Roles.Any(r => r.Id == a.RoleId && r.Active && r.DeletedAt == null))
                || db.UserScopes.Any(s => s.UserId == u.Id && s.Active && s.DeletedAt == null
                    && (s.ScopeType == "EMPRESA" && s.ReferenceId == worker.CompanyId
                        || s.ScopeType == "UNIDADE" && db.Units.Any(l => l.Id == s.ReferenceId && l.CompanyId == worker.CompanyId && l.Active && l.DeletedAt == null)
                        || s.ScopeType == "SETOR" && db.Departments.Any(d => d.Id == s.ReferenceId && d.Active && d.DeletedAt == null
                            && db.Units.Any(l => l.Id == d.UnitId && l.CompanyId == worker.CompanyId && l.Active && l.DeletedAt == null))))));
    private static void RequireUnlinked(Worker worker)
    {
        if (!worker.Active || worker.DeletedAt.HasValue || worker.DismissalDate.HasValue) throw new DomainException("O vínculo profissional não está ativo.");
        if (worker.UserId.HasValue) throw new DomainException("O trabalhador já possui uma conta vinculada.");
    }
    public async Task<WorkerAccountCandidates> SearchAsync(Guid id, string? q, int page, CancellationToken ct)
    {
        var worker = await AuthorizeAsync(id, ct); RequireUnlinked(worker);
        if (page < 1 || page > 1_000_000 || q?.Length > 200) throw new ArgumentException("Busca ou paginação inválida.");
        var query = Candidates(worker);
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.Value.Contains(term)); }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.Name).ThenBy(u => u.Id).Skip((page - 1) * 20).Take(20)
            .Select(u => new WorkerAccountCandidate(u.Id, u.Name, u.Email.Value)).ToArrayAsync(ct);
        return new(worker.Id, worker.CompanyId, worker.UnitId, worker.Revision, items, total, page, 20);
    }
    public async Task<WorkerAccountCandidate> CandidateAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var worker = await AuthorizeAsync(id, ct); RequireUnlinked(worker);
        return await Candidates(worker).Where(u => u.Id == userId).Select(u => new WorkerAccountCandidate(u.Id, u.Name, u.Email.Value)).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Conta indisponível para este vínculo.");
    }
    // AuditLog v2 hashes JSON text; jsonb normalizes it on storage. Hash exactly the
    // PostgreSQL representation here, without rewriting any existing audit record/hash.
    private Task<string> StorageJsonAsync(string json, CancellationToken ct)
        => db.Database.SqlQuery<string>($"SELECT CAST(CAST({json} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
    public async Task<WorkerAccountContext> LinkAsync(Guid id, LinkWorkerAccountRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(id, ct);
        if (!request.Confirmation || request.UserId == Guid.Empty || request.ExpectedVersion < 1) throw new ArgumentException("Confirme a conta e informe a revisão esperada.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Hold the target account against deactivation/identity edits until the binding commits.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM core.usuario WHERE id = {request.UserId} FOR SHARE", ct);
        var worker = await db.Workers.SingleAsync(w => w.Id == id, ct);
        // Context can have changed after authorization; do not apply a stale binding to a moved worker.
        if (worker.Revision != request.ExpectedVersion) throw new DomainException("O trabalhador foi alterado. Atualize o cadastro antes de vincular a conta.");
        await access.WorkerAsync(id, "editar", ct); RequireUnlinked(worker);
        var candidate = await Candidates(worker).Where(u => u.Id == request.UserId).SingleOrDefaultAsync(ct)
            ?? throw new DomainException("A conta não está mais disponível para este vínculo.");
        if (candidate.Name != request.ExpectedAccountName || !string.Equals(candidate.Email.Value, request.ExpectedAccountEmail, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Os dados da conta foram alterados. Confira a pessoa novamente.");
        worker.LinkUser(candidate.Id);
        var previous = await StorageJsonAsync(JsonSerializer.Serialize(new { userId = (Guid?)null, revision = request.ExpectedVersion }), ct);
        var current = await StorageJsonAsync(JsonSerializer.Serialize(new { userId = candidate.Id, revision = worker.Revision }), ct);
        await audit.AddAsync(new AuditLog("ACCOUNT_LINKED", "trabalhador", worker.Id, actor.UserId, actor.Email, actor.Name,
            previous, current, null, actor.IpAddress, actor.UserAgent, await audit.GetLastHashAsync(ct), companyId: worker.CompanyId,
            resourceVersion: worker.Revision, result: "SUCCESS", traceId: actor.TraceId), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Context(worker);
    }
}
