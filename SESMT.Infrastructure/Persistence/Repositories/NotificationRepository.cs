using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Notifications;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.Notifications.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(SesmtDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationDto>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var companies = await ScopedCompaniesAsync(userId, ct);
        return await db.Alerts.Where(a => a.DestinationUserId == userId && a.Active && a.DeletedAt == null
                && ((a.EntityType == "WORK_PERMIT" && db.WorkPermits.Any(p => p.Id == a.EntityId && companies.Contains(p.CompanyId) && p.Active && p.DeletedAt == null))
                    || (a.EntityType == "DOCUMENT" && db.Documents.Any(d => d.Id == a.EntityId && companies.Contains(d.CompanyId) && d.Active && d.DeletedAt == null))))
            .OrderByDescending(a => a.SentAt)
            .Select(a => new NotificationDto(a.Id, a.Type, a.EntityId, a.Title, a.SentAt, a.ReadAt))
            .ToListAsync(ct);
    }

    public async Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var companies = await ScopedCompaniesAsync(userId, ct);
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == notificationId && a.DestinationUserId == userId
            && a.Active && a.DeletedAt == null
            && ((a.EntityType == "WORK_PERMIT" && db.WorkPermits.Any(p => p.Id == a.EntityId && companies.Contains(p.CompanyId) && p.Active && p.DeletedAt == null))
                || (a.EntityType == "DOCUMENT" && db.Documents.Any(d => d.Id == a.EntityId && companies.Contains(d.CompanyId) && d.Active && d.DeletedAt == null))), ct);
        if (alert is null) return false;
        alert.MarkAsRead(userId);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task EnqueueWorkPermitReviewersAsync(WorkPermit permit, Guid sourceEventId, string type, CancellationToken ct)
    {
        if (!permit.AppointmentId.HasValue) return;
        var appointment = await db.WorkPermitAppointments.FirstOrDefaultAsync(a => a.Id == permit.AppointmentId.Value
            && a.CompanyId == permit.CompanyId && a.Active && a.DeletedAt == null, ct);
        if (appointment?.LocationId is not Guid locationId) return;
        var codes = permit.ServiceCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var serviceIds = WorkPermitServiceCatalogEntry.All.Where(s => codes.Contains(s.Code, StringComparer.OrdinalIgnoreCase)).Select(s => s.Id).ToArray();
        if (serviceIds.Length != codes.Length) return;
        var now = DateTimeOffset.UtcNow;
        var recipients = await (from designation in db.OperationalDesignations
            join worker in db.Workers on designation.PersonId equals worker.Id
            where designation.CompanyId == permit.CompanyId && designation.Type == "revisar"
                && designation.Active && designation.DeletedAt == null && designation.RevokedAt == null
                && designation.StartsAt <= now && now < designation.EndsAt
                && designation.LocationIds.Contains(locationId)
                && serviceIds.All(id => designation.ServiceIds.Contains(id))
                && worker.Active && worker.DeletedAt == null && worker.DismissalDate == null
                && worker.UserId.HasValue && db.Users.Any(u => u.Id == worker.UserId.Value && u.Active && u.DeletedAt == null)
                && db.UserScopes.Any(scope => scope.UserId == worker.UserId.Value && scope.ScopeType == "EMPRESA"
                    && scope.ReferenceId == permit.CompanyId && scope.Active && scope.DeletedAt == null)
            select worker.UserId!.Value).Distinct().ToListAsync(ct);
        foreach (var recipientId in recipients)
        {
            if (await db.Alerts.AnyAsync(a => a.SourceEventId == sourceEventId && a.DestinationUserId == recipientId, ct)) continue;
            await db.Alerts.AddAsync(new Alert(type, "WORK_PERMIT", permit.Id, sourceEventId, recipientId,
                "IN_APP", type == "PT_AGUARDANDO_REVISAO" ? "Permissão aguardando revisão" : "Permissão devolvida",
                type == "PT_AGUARDANDO_REVISAO" ? "Uma permissão de trabalho está aguardando revisão." : "Uma permissão de trabalho foi devolvida para correção.", now), ct);
        }
    }

    public async Task EnqueueDocumentReturnAsync(Guid documentId, Guid sourceEventId, Guid recipientUserId, CancellationToken ct)
    {
        if (recipientUserId == Guid.Empty) return;
        var exists = await db.Alerts.AnyAsync(a => a.SourceEventId == sourceEventId && a.DestinationUserId == recipientUserId, ct);
        if (exists) return;
        var document = await db.Documents.Where(d => d.Id == documentId && d.Active && d.DeletedAt == null)
            .Select(d => new { d.CompanyId }).FirstOrDefaultAsync(ct);
        if (document is null || !await db.Users.AnyAsync(u => u.Id == recipientUserId && u.Active && u.DeletedAt == null, ct)
            || !await db.UserScopes.AnyAsync(s => s.UserId == recipientUserId && s.ScopeType == "EMPRESA"
                && s.ReferenceId == document.CompanyId && s.Active && s.DeletedAt == null, ct)) return;
        await db.Alerts.AddAsync(new Alert("DOCUMENTO_DEVOLVIDO", "DOCUMENT", documentId, sourceEventId,
            recipientUserId, "IN_APP", "Documento devolvido", "Um documento foi devolvido para correção.", DateTimeOffset.UtcNow), ct);
    }

    private async Task<List<Guid>> ScopedCompaniesAsync(Guid userId, CancellationToken ct) =>
        await (from scope in db.UserScopes join company in db.Companies on scope.ReferenceId equals company.Id
            where scope.UserId == userId && scope.ScopeType == "EMPRESA" && scope.Active && scope.DeletedAt == null
                && company.Active && company.DeletedAt == null
                && db.Users.Any(u => u.Id == userId && u.Active && u.DeletedAt == null)
            select company.Id).Distinct().ToListAsync(ct);
}
