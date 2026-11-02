using Microsoft.EntityFrameworkCore;
using SESMT.Application.Exceptions;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record TeamManagementContext(Guid CompanyId, Guid LocalId, Guid OrganizationId, string CompanyName, string LocalName);

public sealed class TeamManagementContextService(NativeResourceAccess access, SesmtDbContext db)
{
    public async Task<TeamManagementContext> ReadAsync(Guid locationId, string action, CancellationToken ct)
    {
        if (action is not ("criar" or "editar")) throw new ArgumentException("Selecione uma operação válida para a equipe.");
        var unit = await access.UnitAsync(locationId, "equipe", action, ct);
        if ((await access.ReadAsync(ct)).OwnOnly) throw new ResourceForbiddenException();
        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == unit.CompanyId && c.Active && c.DeletedAt == null, ct);
        return new(company.Id, unit.Id, Team.OrganizationFor(company.Id, company.OrganizationId), company.FantasyName, unit.Name);
    }
}
