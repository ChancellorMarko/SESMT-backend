using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Appointments;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(SesmtDbContext db, IWorkPermitRepository permissions) : IAppointmentRepository
{
    public Task<IReadOnlyList<Guid>> AccessibleCompaniesAsync(Guid actor, CancellationToken ct) => permissions.GetAccessibleCompanyIdsAsync(actor, ct);
    public Task<bool> HasRoleAsync(Guid actor, Guid company, string role, CancellationToken ct) => permissions.HasRoleInCompanyAsync(actor, company, role, ct);
    public Task<bool> ActiveLocationAsync(Guid location, Guid company, CancellationToken ct) => permissions.IsActiveLocationInCompanyAsync(location, company, ct);
    public Task<bool> ActiveUserAsync(Guid user, Guid company, CancellationToken ct) => permissions.IsActiveCompanyUserAsync(user, company, ct);
    public async Task<bool> CanAttendAsync(Guid user, Guid company, Guid location, string[] services, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var people = await db.Workers.Where(w => w.UserId == user && w.CompanyId == company && w.Active && w.DeletedAt == null).Select(w => w.Id).ToArrayAsync(ct);
        var designations = await db.OperationalDesignations.AsNoTracking().Where(d => people.Contains(d.PersonId) && d.CompanyId == company
            && d.Type == "atender" && d.Active && d.DeletedAt == null && d.RevokedAt == null && d.StartsAt <= from && d.EndsAt >= to && d.LocationIds.Contains(location))
            .Select(d => d.ServiceIds).ToListAsync(ct);
        var covered = designations.SelectMany(ids => ids).ToHashSet();
        return services.Length > 0 && services.All(code => WorkPermitServiceCatalogEntry.All.Any(s => s.Code == code && covered.Contains(s.Id)));
    }
    public Task<bool> HasPermitAsync(Guid appointment, CancellationToken ct) => permissions.HasWorkPermitForAppointmentAsync(appointment, ct);
    public async Task<AppointmentTeam?> GetTeamAsync(Guid teamId, Guid company, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId && t.CompanyId == company
            && t.Active && t.DeletedAt == null && db.Companies.Any(c => c.Id == company && c.Active && c.DeletedAt == null), ct);
        if (team is null) return null;
        var members = team.ParticipantIds.Concat(team.ChiefId.HasValue ? new[] {team.ChiefId.Value} : []).Distinct().ToArray();
        var first = DateOnly.FromDateTime(from.UtcDateTime); var last = DateOnly.FromDateTime(to.UtcDateTime);
        var available = await db.Workers.Where(w => members.Contains(w.Id) && w.CompanyId == company && w.Active && w.DeletedAt == null
            && w.AdmissionDate <= first && (w.DismissalDate == null || w.DismissalDate > last)).Select(w => w.Id).ToArrayAsync(ct);
        return new(team.Id, company, team.LocalId, team.Name, available);
    }
    public Task<WorkPermitAppointment?> GetAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct) =>
        db.WorkPermitAppointments.FirstOrDefaultAsync(a => a.Id == id && companies.Contains(a.CompanyId) && a.Active && a.DeletedAt == null, ct);
    public async Task<AppointmentPage<WorkPermitAppointment>> ListAsync(IReadOnlyCollection<Guid> companies, DateTimeOffset? from,
        DateTimeOffset? to, string? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = db.WorkPermitAppointments.AsNoTracking().Where(a => companies.Contains(a.CompanyId) && a.Active && a.DeletedAt == null);
        if (from.HasValue) query = query.Where(a => a.EndsAt > from.Value);
        if (to.HasValue) query = query.Where(a => a.StartsAt < to.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrEmpty(search)) { var text = search.ToLower(); query = query.Where(a => a.Title.ToLower().Contains(text)); }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.StartsAt).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, total, page, pageSize);
    }
    public async Task<(int Location, int Responsible)> OverlapsAsync(Guid except, Guid location, Guid responsible, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var occupied = await db.WorkPermitAppointments.AsNoTracking().Where(a => a.Id != except && a.Active && a.DeletedAt == null
            && a.Status == WorkPermitAppointment.Confirmed && a.StartsAt < to && a.EndsAt > from
            && (a.LocationId == location || a.ResponsibleId == responsible))
            .Select(a => new {a.LocationId, a.ResponsibleId, a.StartsAt, a.EndsAt}).ToListAsync(ct);
        return (AppointmentCapacity.Peak(occupied.Where(a => a.LocationId == location).Select(a => (a.StartsAt, a.EndsAt)), from, to),
            AppointmentCapacity.Peak(occupied.Where(a => a.ResponsibleId == responsible).Select(a => (a.StartsAt, a.EndsAt)), from, to));
    }
    public async Task AddAsync(WorkPermitAppointment appointment, CancellationToken ct) => await db.WorkPermitAppointments.AddAsync(appointment, ct);
    public Task<AppointmentInvite?> FindInviteAsync(string hash, CancellationToken ct) =>
        db.Set<AppointmentInvite>().FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
    public Task<AppointmentInvite?> GetInviteAsync(Guid id, IReadOnlyCollection<Guid> companies, CancellationToken ct) =>
        db.Set<AppointmentInvite>().FirstOrDefaultAsync(i => i.Id == id && companies.Contains(i.CompanyId) && i.Active && i.DeletedAt == null, ct);
    public async Task AddInviteAsync(AppointmentInvite invite, CancellationToken ct) => await db.Set<AppointmentInvite>().AddAsync(invite, ct);
    public Task<int> PendingInviteRequestsAsync(Guid invite, CancellationToken ct) =>
        db.WorkPermitAppointments.CountAsync(a => a.RequestedViaInviteId == invite && a.Status == WorkPermitAppointment.Requested && a.Active && a.DeletedAt == null, ct);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task<T> SerializedAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        // A shared transaction lock also covers revocation versus anonymous request creation.
        // ReadCommitted obtains fresh rows after waiting for the previous reservation commit.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724891352611::bigint)", ct);
        var result = await action(); await transaction.CommitAsync(ct); return result;
    }
}
