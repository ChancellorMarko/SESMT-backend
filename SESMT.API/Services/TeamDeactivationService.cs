using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Exceptions;
using SESMT.Domain.Exceptions;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed class DeactivateTeamRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; set; }
    [Required, StringLength(200, MinimumLength = 3)] public string Reason { get; set; } = "";
}

/// <summary>Blocking appointment. The title is left out on purpose: deactivating a team does not grant agenda reading.</summary>
public sealed record TeamDeactivationBlocker(Guid AppointmentId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status, string? WorkPermitStatus);
public sealed record TeamDeactivationImpact(Guid TeamId, string Name, int Version, bool CanDeactivate,
    int BlockingAppointmentCount, IReadOnlyList<TeamDeactivationBlocker> BlockingAppointments, int ActiveInviteCount);

public sealed class TeamHasPendingAppointmentsException(TeamDeactivationImpact impact)
    : InvalidOperationException("A equipe tem agendamentos futuros ou PTs em andamento. Cancele ou transfira esses agendamentos antes de desativar.")
{
    public TeamDeactivationImpact Impact { get; } = impact;
}

/// <summary>Team deactivation with optimistic concurrency, mandatory reason and audit. Pending work blocks the
/// deactivation instead of being cancelled silently; active public invites of the team are revoked with it.</summary>
public sealed class TeamDeactivationService(SesmtDbContext db, NativeResourceAccess access, ICurrentUserService actor, IAuditLogRepository audit)
{
    private const int MaxListed = 20;
    private static readonly string[] FinishedPermits = [WorkPermitStatus.Completed, WorkPermitStatus.Cancelled];

    public async Task<TeamDeactivationImpact> ReadImpactAsync(Guid teamId, CancellationToken ct)
    {
        var team = await access.TeamAsync(teamId, "excluir", ct);
        return await ImpactAsync(team, DateTimeOffset.UtcNow, ct);
    }

    public async Task<TeamDeactivationImpact> DeactivateAsync(Guid teamId, DeactivateTeamRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim() ?? "";
        if (request.ExpectedVersion < 1 || reason.Length is < 3 or > 200)
            throw new ArgumentException("Informe a revisão esperada e um motivo entre 3 e 200 caracteres.");
        await access.TeamAsync(teamId, "excluir", ct);
        var actorId = actor.UserId ?? throw new UnauthorizedAccessException();

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == teamId && t.Active && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("Equipe não encontrada.");
        if (team.Version != request.ExpectedVersion)
            throw new DomainException("A equipe foi alterada. Atualize a revisão antes de desativar.");
        var now = DateTimeOffset.UtcNow;
        var impact = await ImpactAsync(team, now, ct);
        if (!impact.CanDeactivate) throw new TeamHasPendingAppointmentsException(impact);

        var invites = await db.Set<AppointmentInvite>().Where(i => i.TeamId == team.Id && i.Active && i.DeletedAt == null
            && i.RevokedAt == null && i.ExpiresAt > now).ToListAsync(ct);
        foreach (var invite in invites) invite.Revoke(actorId, invite.Version, now);

        var previous = await StorageJsonAsync(new { team.Name, active = true, revision = team.Version }, ct);
        team.Deactivate(actorId);
        var current = await StorageJsonAsync(new { team.Name, active = false, revision = team.Version, reason,
            revokedInvites = invites.Select(i => i.Id).Order().ToArray() }, ct);
        await audit.AddAsync(new AuditLog("TEAM_DEACTIVATED", "equipe", team.Id, actorId, actor.Email, actor.Name,
            previous, current, null, actor.IpAddress, actor.UserAgent, await audit.GetLastHashAsync(ct), companyId: team.CompanyId,
            resourceVersion: team.Version, result: "SUCCESS", traceId: actor.TraceId), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return impact with { Version = team.Version, CanDeactivate = false, ActiveInviteCount = 0 };
    }

    private async Task<TeamDeactivationImpact> ImpactAsync(Team team, DateTimeOffset now, CancellationToken ct)
    {
        var blocking = from a in db.WorkPermitAppointments.AsNoTracking()
            where a.TeamId == team.Id && a.Active && a.DeletedAt == null && a.Status != WorkPermitAppointment.Cancelled
            let permit = db.WorkPermits.Where(p => p.AppointmentId == a.Id && p.Active && p.DeletedAt == null)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.Status).FirstOrDefault()
            where a.EndsAt > now || permit != null && !FinishedPermits.Contains(permit)
            orderby a.StartsAt, a.Id
            select new TeamDeactivationBlocker(a.Id, a.StartsAt, a.EndsAt, a.Status, permit);
        var count = await blocking.CountAsync(ct);
        var listed = await blocking.Take(MaxListed).ToListAsync(ct);
        var invites = await db.Set<AppointmentInvite>().AsNoTracking().CountAsync(i => i.TeamId == team.Id && i.Active
            && i.DeletedAt == null && i.RevokedAt == null && i.ExpiresAt > now, ct);
        return new(team.Id, team.Name, team.Version, count == 0, count, listed, invites);
    }

    private Task<string> StorageJsonAsync(object value, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(value);
        return db.Database.SqlQuery<string>($"SELECT CAST(CAST({json} AS jsonb) AS text) AS \"Value\"").SingleAsync(ct);
    }
}
