using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Contracts.Browser;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record BrowserWorkerListQuery(Guid UnitId, Guid? CompanyId, string Search, string Status, int Page, int PageSize);

public interface IBrowserWorkersReader
{
    Task<BrowserWorkerPage<BrowserWorkerResponse>> ListAsync(BrowserWorkspaceAccess access, BrowserWorkerListQuery query, CancellationToken ct);
    Task<BrowserWorkerResponse?> FindAsync(BrowserWorkspaceAccess access, Guid id, CancellationToken ct);
    Task<BrowserWorkerPage<BrowserWorkerMovementResponse>> MovementsAsync(BrowserWorkspaceAccess access, BrowserWorkerResponse worker, int page, int pageSize, CancellationToken ct);
}

public static class BrowserWorkersQuery
{
    // Kept as expressions so PostgreSQL filters authority before counting or paginating rows.
    public static IQueryable<Worker> Authorized(IQueryable<Worker> source, BrowserWorkspaceAccess access)
    {
        Expression<Func<Worker, bool>> location = _ => false;
        foreach (var company in access.CompanyIds.Where(company => access.Can("workers.read", company)))
        {
            if (access.IsFullCompany(company))
                location = Or(location, worker => worker.CompanyId == company && worker.UnitId == null && worker.DepartmentId == null);
            foreach (var unit in access.UnitIds.Where(unit => access.UnitCompanies is null
                || access.UnitCompanies.TryGetValue(unit, out var owner) && owner == company))
            {
                var sectors = access.SectorIds.Where(sector => access.SectorUnits is null
                    || access.SectorUnits.TryGetValue(sector, out var parent) && parent == unit).ToArray();
                if (access.FullUnitIds.Contains(unit))
                    location = Or(location, worker => worker.CompanyId == company && worker.UnitId == unit
                        && (worker.DepartmentId == null || sectors.Contains(worker.DepartmentId.Value)));
                else
                    location = Or(location, worker => worker.CompanyId == company && worker.UnitId == unit
                        && worker.DepartmentId.HasValue && sectors.Contains(worker.DepartmentId.Value));
            }
        }
        return source.Where(worker => worker.Active && worker.DeletedAt == null
            && (!access.OwnOnly || worker.Id == access.WorkerId)).Where(location);
    }

    public static IQueryable<Worker> Filter(IQueryable<Worker> source, BrowserWorkerListQuery query, DateOnly today)
    {
        source = source.Where(worker => worker.UnitId == query.UnitId);
        if (query.CompanyId.HasValue) source = source.Where(worker => worker.CompanyId == query.CompanyId.Value);
        if (query.Status == "ACTIVE") source = source.Where(worker => worker.DismissalDate == null || worker.DismissalDate > today);
        else if (query.Status == "TERMINATED") source = source.Where(worker => worker.DismissalDate != null && worker.DismissalDate <= today);
        if (query.Search.Length > 0)
        {
            var term = query.Search.ToUpperInvariant();
            source = source.Where(worker => worker.Name.Value.ToUpper().Contains(term) || worker.Enrollment.ToUpper().Contains(term));
        }
        return source.OrderBy(worker => worker.Name.Value).ThenBy(worker => worker.Id);
    }

    public static string Status(Worker worker, DateOnly today) => worker.DismissalDate is { } date && date <= today ? "TERMINATED" : "ACTIVE";

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);

    private static Expression<Func<Worker, bool>> Or(Expression<Func<Worker, bool>> left, Expression<Func<Worker, bool>> right) =>
        Expression.Lambda<Func<Worker, bool>>(Expression.OrElse(left.Body,
            new ParameterSubstitution(right.Parameters[0], left.Parameters[0]).Visit(right.Body)), left.Parameters);

    private sealed class ParameterSubstitution(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression parameter) => parameter == source ? target : parameter;
    }
}

public sealed class BrowserWorkersReader(SesmtDbContext db) : IBrowserWorkersReader
{
    public async Task<BrowserWorkerPage<BrowserWorkerResponse>> ListAsync(BrowserWorkspaceAccess access, BrowserWorkerListQuery query, CancellationToken ct)
    {
        var today = BrowserWorkersQuery.Today();
        var rows = BrowserWorkersQuery.Filter(BrowserWorkersQuery.Authorized(db.Workers.AsNoTracking(), access), query, today);
        var total = await rows.CountAsync(ct);
        var items = await Project(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize), today).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<BrowserWorkerResponse?> FindAsync(BrowserWorkspaceAccess access, Guid id, CancellationToken ct) =>
        await Project(BrowserWorkersQuery.Authorized(db.Workers.AsNoTracking(), access).Where(worker => worker.Id == id), BrowserWorkersQuery.Today()).SingleOrDefaultAsync(ct);

    public async Task<BrowserWorkerPage<BrowserWorkerMovementResponse>> MovementsAsync(BrowserWorkspaceAccess access, BrowserWorkerResponse worker, int page, int pageSize, CancellationToken ct)
    {
        var fullUnits = access.FullUnitIds.ToArray();
        var sectors = access.SectorIds.ToArray();
        var query = from history in db.WorkerHistories.AsNoTracking()
            join department in db.Departments.AsNoTracking() on history.DepartmentId equals department.Id
            join unit in db.Units.AsNoTracking() on department.UnitId equals unit.Id
            join position in db.Positions.AsNoTracking() on history.PositionId equals position.Id
            where history.WorkerId == worker.Id && history.Active && history.DeletedAt == null
                && department.DeletedAt == null && unit.DeletedAt == null && position.DeletedAt == null
                && unit.CompanyId == worker.CompanyId && position.CompanyId == worker.CompanyId
                && (fullUnits.Contains(unit.Id) || sectors.Contains(department.Id))
            orderby history.StartDate descending, history.Id descending
            select new BrowserWorkerMovementResponse(history.Id, history.WorkerId, "ASSIGNMENT", history.StartDate,
                null, new BrowserWorkerAssignment(unit.Id, department.Id, position.Id), history.ReasonForChange, history.CreatedAt, history.CreatedBy);
        var total = await query.CountAsync(ct);
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), page, pageSize, total);
    }

    private static IQueryable<BrowserWorkerResponse> Project(IQueryable<Worker> rows, DateOnly today) => rows.Select(worker =>
        new BrowserWorkerResponse(worker.Id, worker.Enrollment, worker.Name.Value, worker.CompanyId, worker.UnitId,
            worker.DepartmentId, worker.PositionId, worker.AdmissionDate,
            worker.DismissalDate != null && worker.DismissalDate <= today ? "TERMINATED" : "ACTIVE"));
}
