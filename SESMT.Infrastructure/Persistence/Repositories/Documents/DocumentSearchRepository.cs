using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Mappers;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Repositories.Documents;

public sealed class DocumentSearchRepository(SesmtDbContext db) : IDocumentSearchRepository
{
    public async Task<DocumentPageResponse> SearchAsync(DocumentSearchRequest r, Expression<Func<Document, bool>> authorized, CancellationToken ct)
    {
        // Authorization and secrecy restrictions precede every filter/count/page.
        var query = db.Documents.AsNoTracking().Where(authorized)
            .Where(d => d.SecrecyLevel == SecrecyLevel.Public || d.SecrecyLevel == SecrecyLevel.Internal)
            .Where(d => !db.DocumentTypes.Any(t => t.Id == d.DocumentTypeId && (t.SecrecyLevel == "Medico" || t.SecrecyLevel == "MEDICO" || t.SecrecyLevel == "Restrito" || t.SecrecyLevel == "RESTRITO")))
            .Where(d => !d.UnitId.HasValue || db.Units.Any(u => u.Id == d.UnitId && u.CompanyId == d.CompanyId && u.Active && u.DeletedAt == null))
            .Where(d => !d.DepartmentId.HasValue || db.Departments.Any(department => department.Id == d.DepartmentId && department.UnitId == d.UnitId && department.Active && department.DeletedAt == null));
        if (r.CompanyId.HasValue) query = query.Where(d => d.CompanyId == r.CompanyId);
        if (r.UnitId.HasValue) query = query.Where(d => d.UnitId == r.UnitId);
        if (r.DepartmentId.HasValue) query = query.Where(d => d.DepartmentId == r.DepartmentId);
        if (r.DocumentTypeId.HasValue) query = query.Where(d => d.DocumentTypeId == r.DocumentTypeId);
        if (!string.IsNullOrWhiteSpace(r.Status)) query = query.Where(d => d.Status == r.Status);
        if (!string.IsNullOrWhiteSpace(r.SecrecyLevel)) query = query.Where(d => d.SecrecyLevel == r.SecrecyLevel);
        if (r.From.HasValue) query = query.Where(d => d.IssueDate >= r.From);
        if (r.To.HasValue) query = query.Where(d => d.IssueDate <= r.To);
        if (r.ValidUntilBefore.HasValue) query = query.Where(d => d.ValidUntilDate != null && d.ValidUntilDate < r.ValidUntilBefore);
        if (r.PersonId.HasValue) query = query.Where(d => db.DocumentLinks.Any(l => l.DocumentId == d.Id && l.Active && l.DeletedAt == null && l.EntityType == LinkedEntityType.Worker && l.EntityId == r.PersonId));
        if (r.TeamId.HasValue) query = query.Where(d => db.DocumentLinks.Any(l => l.DocumentId == d.Id && l.Active && l.DeletedAt == null && l.EntityType == LinkedEntityType.Worker
            && db.Teams.Any(t => t.Id == r.TeamId && t.CompanyId == d.CompanyId && t.Active && t.DeletedAt == null && (t.ParticipantIds.Contains(l.EntityId) || t.ChiefId == l.EntityId))));
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var escaped = r.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var term = $"%{escaped}%";
            query = query.Where(d => EF.Functions.ILike(d.Title, term, "\\") || EF.Functions.ILike(d.Code, term, "\\"));
        }
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.IssueDate).ThenBy(d => d.Id).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).ToListAsync(ct);
        return new(items.Select(DocumentMapper.ToSummary).ToArray(), r.Page, r.PageSize, count);
    }
}
