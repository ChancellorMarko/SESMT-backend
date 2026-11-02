using Microsoft.EntityFrameworkCore;

namespace SESMT.Infrastructure.Persistence.Queries;

/// <summary>Explicit database assignment plus the requested resource/action; JWT labels never grant scope.</summary>
public static class GlobalAdministrationScope
{
    public static async Task<Guid[]> CompanyIdsAsync(SesmtDbContext db, Guid actorId, string resource, string action, CancellationToken ct)
    {
        var allowed = await (from a in db.UserRoles join r in db.Roles on a.RoleId equals r.Id
            join rp in db.RolePermissions on r.Id equals rp.RoleId join p in db.Permissions on rp.PermissionId equals p.Id
            join u in db.Users on a.UserId equals u.Id
            where a.UserId == actorId && a.CompanyId == null && a.Active && a.DeletedAt == null
                && u.Active && u.DeletedAt == null && r.Code == "ADMIN" && r.Active && r.DeletedAt == null
                && rp.Active && rp.DeletedAt == null && p.Active && p.DeletedAt == null && p.Resource == resource && p.Action == action
            select a.Id).AnyAsync(ct);
        return allowed ? await db.Companies.Where(c => c.Active && c.DeletedAt == null).Select(c => c.Id).ToArrayAsync(ct) : [];
    }
}
