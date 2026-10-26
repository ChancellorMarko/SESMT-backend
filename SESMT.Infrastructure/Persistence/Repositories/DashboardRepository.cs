using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Dashboard;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Domain.Modules.WorkPermits.Constants;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(SesmtDbContext db) : IDashboardRepository
{
    public async Task<DashboardData> GetDataAsync(Guid actorId, CancellationToken ct, DashboardFilter? activityFilter = null)
    {
        var now = DateTimeOffset.UtcNow;
        var companies = await (from scope in db.UserScopes
            join company in db.Companies on scope.ReferenceId equals company.Id
            where scope.UserId == actorId && scope.ScopeType == ScopeType.EMPRESA && scope.Active && scope.DeletedAt == null
                && company.Active && company.DeletedAt == null
                && db.Users.Any(user => user.Id == actorId && user.Active && user.DeletedAt == null)
            select company.Id).Distinct().ToListAsync(ct);
        if (companies.Count == 0) return new([], [], [], new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(), []);

        var actorWorkers = await db.Workers.Where(w => w.UserId == actorId && companies.Contains(w.CompanyId)
                && w.Active && w.DeletedAt == null && w.DismissalDate == null)
            .Select(w => w.Id).ToListAsync(ct);
        var designations = await db.OperationalDesignations.Where(x => companies.Contains(x.CompanyId)
                && x.Active && x.DeletedAt == null && x.RevokedAt == null && x.StartsAt <= now && now < x.EndsAt)
            .ToListAsync(ct);
        var mine = designations.Where(x => actorWorkers.Contains(x.PersonId)).ToArray();
        var actorTeams = await db.Teams.Where(t => companies.Contains(t.CompanyId ?? Guid.Empty) && t.Active && t.DeletedAt == null
                && t.ChiefId.HasValue && actorWorkers.Contains(t.ChiefId.Value))
            .ToListAsync(ct);
        var teamIds = actorTeams.Select(t => t.Id).ToHashSet();
        var teamLocations = actorTeams.Where(t => t.LocalId.HasValue).Select(t => t.LocalId!.Value).ToHashSet();
        var teamWorkerIds = actorTeams.SelectMany(t => t.ParticipantIds).Concat(actorTeams.Where(t => t.ChiefId.HasValue).Select(t => t.ChiefId!.Value)).Distinct().ToArray();
        var teamUserIds = await db.Workers.Where(w => teamWorkerIds.Contains(w.Id) && w.Active && w.DeletedAt == null
                && w.UserId.HasValue && companies.Contains(w.CompanyId))
            .Select(w => w.UserId!.Value).Distinct().ToListAsync(ct);
        var localIds = mine.SelectMany(d => d.LocationIds).Concat(teamLocations).Distinct().ToArray();

        var appointments = await db.WorkPermitAppointments.Where(a => companies.Contains(a.CompanyId)
                && a.Active && a.DeletedAt == null)
            .OrderBy(a => a.StartsAt).ToListAsync(ct);
        var permits = await db.WorkPermits.Include(p => p.Participants).Where(p => companies.Contains(p.CompanyId)
                && p.Active && p.DeletedAt == null)
            .ToListAsync(ct);
        var permitByAppointment = permits.Where(p => p.AppointmentId.HasValue)
            .GroupBy(p => p.AppointmentId!.Value).ToDictionary(g => g.Key, g => g.First());

        var visibleAppointments = appointments.Where(a => a.RequestedBy == actorId
            || (a.LocationId.HasValue && mine.Any(d => d.LocationIds.Contains(a.LocationId.Value)
                && d.Type is ("atender" or "liberar" or "revisar")
                && (!permitByAppointment.TryGetValue(a.Id, out var designatedPermit)
                    || (ServiceIdsFor(designatedPermit.ServiceCode).Length > 0
                        && ServiceIdsFor(designatedPermit.ServiceCode).All(id => d.ServiceIds.Contains(id))))))
            || (a.LocationId.HasValue && permitByAppointment.TryGetValue(a.Id, out var teamPermit)
                && TeamIdsFor(teamPermit, a.LocationId.Value).Length > 0))
            .ToArray();
        var appointmentRows = new List<DashboardAppointment>();
        foreach (var appointment in visibleAppointments)
        {
            permitByAppointment.TryGetValue(appointment.Id, out var permit);
            var rowTeamIds = permit is null || appointment.LocationId is not Guid location ? [] : TeamIdsFor(permit, location);
            var serviceIds = permit is null ? [] : ServiceIdsFor(permit.ServiceCode);
            var reviewStatus = permit is not null && appointment.LocationId.HasValue && mine.Any(d => d.Type == "revisar"
                && d.LocationIds.Contains(appointment.LocationId.Value) && serviceIds.Length > 0
                && serviceIds.All(id => d.ServiceIds.Contains(id))) ? ReviewStatus(permit.Status) : null;
            appointmentRows.Add(new(appointment.Id, permit?.Id, appointment.Title, appointment.CompanyId,
                appointment.LocationId, appointment.StartsAt, appointment.EndsAt, permit?.Status,
                reviewStatus, serviceIds, rowTeamIds));
        }

        var visiblePermits = permits.Where(p =>
        {
            if (p.CreatedBy == actorId || p.Participants.Any(x => x.UserId == actorId)) return true;
            var appointment = appointments.FirstOrDefault(a => a.Id == p.AppointmentId);
            if (appointment?.LocationId is not Guid location) return false;
            if (TeamIdsFor(p, location).Length > 0) return true;
            var serviceIds = ServiceIdsFor(p.ServiceCode);
            return mine.Any(d => d.LocationIds.Contains(location) && serviceIds.Length > 0
                && serviceIds.All(id => d.ServiceIds.Contains(id)) && d.Type is ("atender" or "liberar" or "revisar"));
        }).ToArray();

        var visiblePermitIds = visiblePermits.Select(p => p.Id).ToArray();
        var transitionRows = visiblePermitIds.Length == 0 || activityFilter is null ? [] : await (from transition in db.WorkPermitTransitions
            join permit in db.WorkPermits on transition.WorkPermitId equals permit.Id
            join appointment in db.WorkPermitAppointments on permit.AppointmentId equals appointment.Id into appointmentJoin
            from appointment in appointmentJoin.DefaultIfEmpty()
            where visiblePermitIds.Contains(transition.WorkPermitId)
                && (!activityFilter.From.HasValue || transition.OccurredAt >= activityFilter.From.Value)
                && (!activityFilter.To.HasValue || transition.OccurredAt <= activityFilter.To.Value)
            select new
            {
                transition.OccurredAt,
                transition.NewStatus,
                LocalId = appointment == null ? null : appointment.LocationId,
                permit.ServiceCode,
                PermitId = permit.Id
            }).ToListAsync(ct);
        var activityTransitions = transitionRows.Select(row => new DashboardActivityTransition(
            row.OccurredAt,
            row.NewStatus,
            row.LocalId,
            ServiceIdsFor(row.ServiceCode),
            row.LocalId is Guid activityLocation && permits.FirstOrDefault(p => p.Id == row.PermitId) is { } activityPermit
                ? TeamIdsFor(activityPermit, activityLocation)
                : [])).ToArray();
        var activeRequirements = await db.WorkPermitDocumentRequirements.Where(r => companies.Contains(r.CompanyId)
                && r.Active && r.DeletedAt == null)
            .Select(r => new { r.CompanyId, r.ServiceCode, r.DocumentTypeId }).ToListAsync(ct);
        var linkedEvidenceTypes = visiblePermitIds.Length == 0 ? [] : await (from link in db.DocumentLinks
            join permit in db.WorkPermits on link.EntityId equals permit.Id
            join document in db.Documents on link.DocumentId equals document.Id
            where visiblePermitIds.Contains(link.EntityId) && link.EntityType == LinkedEntityType.WorkPermit
                && permit.CompanyId == document.CompanyId && permit.Active && permit.DeletedAt == null
                && link.Active && link.DeletedAt == null && document.Status == DocumentStatus.Active
                && document.Active && document.DeletedAt == null
                && (document.ValidFromDate == null || document.ValidFromDate <= DateOnly.FromDateTime(now.UtcDateTime))
                && (document.ValidUntilDate == null || document.ValidUntilDate >= DateOnly.FromDateTime(now.UtcDateTime))
            select new { PermitId = link.EntityId, document.DocumentTypeId }).ToListAsync(ct);
        var linkedEvidenceByPermit = linkedEvidenceTypes.GroupBy(x => x.PermitId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.DocumentTypeId).ToHashSet());

        var documentPendencies = new List<DashboardDocumentPendency>();
        foreach (var permit in visiblePermits)
        {
            if (permit.Status is WorkPermitStatus.Cancelled) continue;
            var codes = permit.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var requirements = activeRequirements.Where(r => r.CompanyId == permit.CompanyId && codes.Contains(r.ServiceCode))
                .Select(r => r.DocumentTypeId).ToHashSet();
            linkedEvidenceByPermit.TryGetValue(permit.Id, out var linkedTypeIds);
            var pending = requirements.Except(linkedTypeIds ?? new HashSet<Guid>()).Count();
            if (pending == 0) continue;
            var appointment = appointments.FirstOrDefault(a => a.Id == permit.AppointmentId);
            var permitTeams = appointment?.LocationId is Guid permitLocation ? TeamIdsFor(permit, permitLocation) : [];
            documentPendencies.Add(new(permit.Id, permit.CompanyId, appointment?.LocationId, permit.Status, pending,
                permit.CreatedAt, ServiceIdsFor(permit.ServiceCode), permitTeams));
        }

        var reviews = new List<DashboardReviewPendency>();
        foreach (var permit in visiblePermits.Where(p => p.Status == WorkPermitStatus.Completed || p.Status == "DEVOLVIDA"))
        {
            var appointment = appointments.FirstOrDefault(a => a.Id == permit.AppointmentId);
            if (appointment?.LocationId is not Guid location) continue;
            var codes = permit.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var serviceIds = ServiceIdsFor(permit.ServiceCode);
            if (!mine.Any(d => d.Type == "revisar" && d.LocationIds.Contains(location) && serviceIds.Length > 0 && serviceIds.All(id => d.ServiceIds.Contains(id)))) continue;
            reviews.Add(new(permit.Id, permit.CompanyId, location, codes, permit.Version, permit.CompletedAt, TeamIdsFor(permit, location)));
        }

        var ownAppointmentLocations = appointments.Where(a => a.RequestedBy == actorId && a.LocationId.HasValue).Select(a => a.LocationId!.Value);
        var allowedServiceIds = mine.SelectMany(d => d.ServiceIds).Concat(visiblePermits.SelectMany(p => ServiceIdsFor(p.ServiceCode))).ToHashSet();
        var permittedLocations = visiblePermits.Select(p => appointments.FirstOrDefault(a => a.Id == p.AppointmentId)?.LocationId)
            .Where(id => id.HasValue).Select(id => id!.Value);
        return new(appointmentRows, documentPendencies, reviews,
            localIds.Concat(ownAppointmentLocations).Concat(permittedLocations).ToHashSet(), teamIds, allowedServiceIds,
            activityTransitions);

        Guid[] TeamIdsFor(WorkPermit permit, Guid locationId) => actorTeams
            .Where(team => team.LocalId == locationId
                && (permit.CreatedBy == actorId || permit.Participants.Any(participant => teamUserIds.Contains(participant.UserId))))
            .Select(team => team.Id).ToArray();
    }

    private static Guid[] ServiceIdsFor(string serviceCode)
    {
        var codes = serviceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return WorkPermitServiceCatalogEntry.All.Where(s => codes.Contains(s.Code, StringComparer.OrdinalIgnoreCase)).Select(s => s.Id).ToArray();
    }

    private static string? ReviewStatus(string? status) => status switch
    {
        WorkPermitStatus.Completed => "AGUARDANDO_REVISAO",
        "DEVOLVIDA" => "DEVOLVIDA",
        "APROVADA" => "APROVADA",
        _ => null
    };
}
