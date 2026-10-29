using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public sealed class DocumentAccessPolicy(SesmtDbContext db) : IDocumentAccessPolicy
{
    public async Task<Expression<Func<Document, bool>>> ReadFilterAsync(Guid actorId, CancellationToken ct) =>
        await FilterAsync(actorId, "ler", ct);

    public async Task<bool> CanAccessAsync(Guid actorId, Document document, string action, CancellationToken ct)
    {
        if (document.UnitId.HasValue && !await db.Units.AnyAsync(u => u.Id == document.UnitId && u.CompanyId == document.CompanyId && u.Active && u.DeletedAt == null, ct)) return false;
        if (document.DepartmentId.HasValue && (!document.UnitId.HasValue || !await db.Departments.AnyAsync(d => d.Id == document.DepartmentId && d.UnitId == document.UnitId && d.Active && d.DeletedAt == null, ct))) return false;
        return (await FilterAsync(actorId, action, ct)).Compile()(document);
    }

    private async Task<Expression<Func<Document, bool>>> FilterAsync(Guid actorId, string action, CancellationToken ct, bool requireProtectedGrant = true)
    {
        if (!await db.Users.AnyAsync(u => u.Id == actorId && u.Active && u.DeletedAt == null, ct)) return _ => false;
        var scopes = await db.UserScopes.Where(s => s.UserId == actorId && s.Active && s.DeletedAt == null).ToListAsync(ct);
        var companyScopeIds = scopes.Where(s => s.ScopeType == "EMPRESA" && s.ReferenceId.HasValue).Select(s => s.ReferenceId!.Value).ToArray();
        var unitScopeIds = scopes.Where(s => s.ScopeType == "UNIDADE" && s.ReferenceId.HasValue).Select(s => s.ReferenceId!.Value).ToArray();
        var departmentScopeIds = scopes.Where(s => s.ScopeType == "SETOR" && s.ReferenceId.HasValue).Select(s => s.ReferenceId!.Value).ToArray();
        var units = await db.Units.Where(u => u.Active && u.DeletedAt == null).Select(u => new { u.Id, u.CompanyId }).ToListAsync(ct);
        var departments = await db.Departments.Where(d => d.Active && d.DeletedAt == null).Select(d => new { d.Id, d.UnitId }).ToListAsync(ct);
        unitScopeIds = unitScopeIds.Where(id => units.Any(u => u.Id == id)).ToArray();
        departmentScopeIds = departmentScopeIds.Where(id => departments.Any(d => d.Id == id && units.Any(u => u.Id == d.UnitId))).ToArray();
        var scopeCompanies = companyScopeIds.Concat(units.Where(u => unitScopeIds.Contains(u.Id)
            || departments.Any(d => departmentScopeIds.Contains(d.Id) && d.UnitId == u.Id)).Select(u => u.CompanyId)).Distinct().ToArray();
        var activeCompanies = await db.Companies.Where(c => scopeCompanies.Contains(c.Id) && c.Active && c.DeletedAt == null).Select(c => c.Id).ToArrayAsync(ct);
        var permissions = await (from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            join rp in db.RolePermissions on r.Id equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == actorId && ur.Active && ur.DeletedAt == null && r.Active && r.DeletedAt == null
                && rp.Active && rp.DeletedAt == null && p.Active && p.DeletedAt == null
            select new { ur.CompanyId, r.Code, p.Resource, p.Action }).ToListAsync(ct);
        bool Has(Guid companyId, string resource, string permission) => permissions.Any(p => (!p.CompanyId.HasValue || p.CompanyId == companyId) && p.Resource == resource && p.Action == permission);
        var allowed = activeCompanies.Where(id => Has(id, "documento", action)).ToArray();
        var workerOnly = allowed.Where(id => permissions.Where(p => (!p.CompanyId.HasValue || p.CompanyId == id) && p.Resource == "documento" && p.Action == action).All(p => p.Code == "TRABALHADOR")).ToArray();
        var leaderOnly = allowed.Where(id => permissions.Where(p => (!p.CompanyId.HasValue || p.CompanyId == id) && p.Resource == "documento" && p.Action == action).All(p => p.Code is "LIDER" or "TRABALHADOR")).ToArray();
        var selfWorkers = await db.Workers.Where(w => w.UserId == actorId && w.Active && w.DeletedAt == null).Select(w => w.Id).ToArrayAsync(ct);
        var selfDocs = await db.DocumentLinks.Where(l => l.Active && l.DeletedAt == null && l.EntityType == LinkedEntityType.Worker && selfWorkers.Contains(l.EntityId)).Select(l => l.DocumentId).ToArrayAsync(ct);
        var ledWorkers = await db.Teams.Where(t => t.Active && t.DeletedAt == null && t.ChiefId.HasValue && selfWorkers.Contains(t.ChiefId.Value))
            .Select(t => t.ParticipantIds).ToListAsync(ct);
        var ledWorkerIds = ledWorkers.SelectMany(x => x).Concat(selfWorkers).Distinct().ToArray();
        var ledDocs = await db.DocumentLinks.Where(l => l.Active && l.DeletedAt == null && l.EntityType == LinkedEntityType.Worker && ledWorkerIds.Contains(l.EntityId)).Select(l => l.DocumentId).ToArrayAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var grants = await db.Set<DocumentAccessGrant>().Where(g => g.UserId == actorId && g.Active && g.DeletedAt == null && g.RevokedAt == null && g.ExpiresAt > now).Select(g => g.DocumentId).ToArrayAsync(ct);
        var medical = allowed.Where(id => Has(id, "aso", action == "ler" ? "ler" : "editar")).ToArray();
        var medicalTypes = await db.DocumentTypes.Where(t => t.SecrecyLevel == "Medico" || t.SecrecyLevel == "MEDICO").Select(t => t.Id).ToArrayAsync(ct);
        var restrictedTypes = await db.DocumentTypes.Where(t => t.SecrecyLevel == "Restrito" || t.SecrecyLevel == "RESTRITO").Select(t => t.Id).ToArrayAsync(ct);
        return d => d.Active && d.DeletedAt == null && allowed.Contains(d.CompanyId)
            && (companyScopeIds.Contains(d.CompanyId) || d.UnitId.HasValue && unitScopeIds.Contains(d.UnitId.Value) || d.DepartmentId.HasValue && departmentScopeIds.Contains(d.DepartmentId.Value))
            && (!workerOnly.Contains(d.CompanyId) || selfDocs.Contains(d.Id))
            && (!leaderOnly.Contains(d.CompanyId) || ledDocs.Contains(d.Id))
            && (!requireProtectedGrant
                || (d.SecrecyLevel == SecrecyLevel.Public || d.SecrecyLevel == SecrecyLevel.Internal) && !medicalTypes.Contains(d.DocumentTypeId) && !restrictedTypes.Contains(d.DocumentTypeId)
                || (d.SecrecyLevel == SecrecyLevel.Restricted || restrictedTypes.Contains(d.DocumentTypeId)) && d.SecrecyLevel != SecrecyLevel.Medical && !medicalTypes.Contains(d.DocumentTypeId) && grants.Contains(d.Id)
                || (d.SecrecyLevel == SecrecyLevel.Medical || medicalTypes.Contains(d.DocumentTypeId)) && medical.Contains(d.CompanyId) && grants.Contains(d.Id));
    }

    public async Task<bool> ValidateContextAsync(Guid actorId, Document document, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct)
    {
        // Context is validated independently of document existence/grants, before creation.
        var plain = document.AdministrativeCopy();
        if (!(await FilterAsync(actorId, "criar", ct, false)).Compile()(plain)) return false;
        if (!await db.Companies.AnyAsync(c => c.Id == document.CompanyId && c.Active && c.DeletedAt == null, ct)
            || !await db.Users.AnyAsync(u => u.Id == document.OwnerId && u.Active && u.DeletedAt == null, ct)) return false;
        if (document.UnitId.HasValue && !await db.Units.AnyAsync(u => u.Id == document.UnitId && u.CompanyId == document.CompanyId && u.Active && u.DeletedAt == null, ct)) return false;
        if (document.DepartmentId.HasValue && (!document.UnitId.HasValue || !await db.Departments.AnyAsync(d => d.Id == document.DepartmentId && d.UnitId == document.UnitId && d.Active && d.DeletedAt == null, ct))) return false;
        var ownerFilter = await FilterAsync(document.OwnerId, "ler", ct, false);
        if (!ownerFilter.Compile()(plain)) return false;
        foreach (var link in links)
        {
            if (link.EntityId == Guid.Empty) return false;
            var valid = link.EntityType switch
            {
                LinkedEntityType.Worker => await db.Workers.AnyAsync(w => w.Id == link.EntityId && w.CompanyId == document.CompanyId && w.Active && w.DeletedAt == null
                    && (!document.UnitId.HasValue || w.UnitId == document.UnitId) && (!document.DepartmentId.HasValue || w.DepartmentId == document.DepartmentId), ct),
                LinkedEntityType.Company => link.EntityId == document.CompanyId,
                LinkedEntityType.Unit => link.EntityId == document.UnitId,
                LinkedEntityType.Department => link.EntityId == document.DepartmentId,
                LinkedEntityType.WorkPermit => await db.WorkPermits.AnyAsync(p => p.Id == link.EntityId && p.CompanyId == document.CompanyId && p.Active && p.DeletedAt == null),
                _ => false,
            };
            if (!valid) return false;
        }
        if (document.SecrecyLevel is SecrecyLevel.Restricted or SecrecyLevel.Medical)
            return await CanGrantAsync(actorId, document, ct);
        return true;
    }

    private async Task<bool> DesignatedAsync(Guid actorId, Document document, CancellationToken ct)
    {
        if (!document.UnitId.HasValue) return false;
        var now = DateTimeOffset.UtcNow;
        return await (from designation in db.OperationalDesignations
            join worker in db.Workers on designation.PersonId equals worker.Id
            where worker.UserId == actorId && worker.CompanyId == document.CompanyId && worker.Active && worker.DeletedAt == null && worker.DismissalDate == null
                && designation.CompanyId == document.CompanyId && designation.Type == "conferir"
                && designation.Active && designation.DeletedAt == null && designation.RevokedAt == null
                && designation.StartsAt <= now && designation.EndsAt > now && designation.LocationIds.Contains(document.UnitId.Value)
            select designation.Id).AnyAsync(ct);
    }
    public async Task<bool> CanGrantAsync(Guid actorId, Document document, CancellationToken ct)
    {
        var plain = document.AdministrativeCopy();
        if (!(await FilterAsync(actorId, "aprovar", ct, false)).Compile()(plain) || !await DesignatedAsync(actorId, document, ct)) return false;
        if (await EffectiveSecrecyAsync(document, ct) != SecrecyLevel.Medical) return true;
        return await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id join rp in db.RolePermissions on ur.RoleId equals rp.RoleId join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == actorId && (ur.CompanyId == null || ur.CompanyId == document.CompanyId) && ur.Active && ur.DeletedAt == null
                && role.Active && role.DeletedAt == null
                && rp.Active && rp.DeletedAt == null && p.Active && p.DeletedAt == null && p.Resource == "aso" && p.Action == "editar" select ur.Id).AnyAsync(ct);
    }
    public async Task<bool> CanReviewAsync(Guid actorId, Document document, CancellationToken ct)
    {
        if (!await CanAccessAsync(actorId, document, "aprovar", ct) || !await DesignatedAsync(actorId, document, ct)) return false;
        if (await EffectiveSecrecyAsync(document, ct) is not (SecrecyLevel.Restricted or SecrecyLevel.Medical)) return true;
        var now = DateTimeOffset.UtcNow;
        return await db.Set<DocumentAccessGrant>().AnyAsync(g => g.DocumentId == document.Id && g.UserId == actorId && g.CanReview && g.Active && g.DeletedAt == null && g.RevokedAt == null && g.ExpiresAt > now, ct);
    }
    public async Task<bool> CanReceiveGrantAsync(Guid userId, Document document, CancellationToken ct)
    {
        var plain = document.AdministrativeCopy();
        if (!(await FilterAsync(userId, "ler", ct, false)).Compile()(plain)) return false;
        if (await EffectiveSecrecyAsync(document, ct) != SecrecyLevel.Medical) return true;
        return await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id join rp in db.RolePermissions on ur.RoleId equals rp.RoleId join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == userId && (ur.CompanyId == null || ur.CompanyId == document.CompanyId) && ur.Active && ur.DeletedAt == null
                && role.Active && role.DeletedAt == null
                && rp.Active && rp.DeletedAt == null && p.Active && p.DeletedAt == null && p.Resource == "aso" && p.Action == "ler" select ur.Id).AnyAsync(ct);
    }
    public async Task AddLinksAsync(Guid documentId, IReadOnlyCollection<DocumentLinkCreateRequest> links, CancellationToken ct)
    {
        foreach (var link in links.DistinctBy(l => (l.EntityType, l.EntityId)))
            await db.DocumentLinks.AddAsync(new DocumentLink(documentId, link.EntityType, link.EntityId), ct);
    }
    public async Task AddGrantAsync(DocumentAccessGrant grant, CancellationToken ct) => await db.Set<DocumentAccessGrant>().AddAsync(grant, ct);
    public Task<DocumentAccessGrant?> GetGrantAsync(Guid grantId, Guid documentId, CancellationToken ct) => db.Set<DocumentAccessGrant>().FirstOrDefaultAsync(g => g.Id == grantId && g.DocumentId == documentId && g.DeletedAt == null, ct);

    private async Task<string> EffectiveSecrecyAsync(Document document, CancellationToken ct)
    {
        var type = await db.DocumentTypes.Where(t => t.Id == document.DocumentTypeId).Select(t => t.SecrecyLevel).FirstOrDefaultAsync(ct);
        if (document.SecrecyLevel == SecrecyLevel.Medical || string.Equals(type, SecrecyLevel.Medical, StringComparison.OrdinalIgnoreCase)) return SecrecyLevel.Medical;
        if (document.SecrecyLevel == SecrecyLevel.Restricted || string.Equals(type, SecrecyLevel.Restricted, StringComparison.OrdinalIgnoreCase)) return SecrecyLevel.Restricted;
        return document.SecrecyLevel;
    }
}
