using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Contracts.Browser;
using SESMT.API.Services;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("browser/organization")]
public sealed class BrowserOrganizationController(IBrowserWorkspaceAccessReader accessReader, SesmtDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Failure(401, "UNAUTHENTICATED", "Entre novamente para continuar.");
        var access = await accessReader.ReadAsync(userId, cancellationToken);
        if (access is null) return Failure(403, "FORBIDDEN", "Seu acesso não permite consultar a organização.");
        var allowedCompanies = access.CompanyIds.Where(company => access.Can("organization.read", company)).ToArray();
        if (allowedCompanies.Length == 0) return Failure(403, "FORBIDDEN", "Seu acesso não permite consultar a organização.");
        Guid? selectedUnit = null;
        if (Request.Query.TryGetValue("unitId", out var queryUnit))
        {
            if (queryUnit.Count != 1 || !Guid.TryParse(queryUnit[0], out var parsed) || parsed == Guid.Empty)
                return Failure(422, "VALIDATION_FAILED", "Informe uma unidade válida.");
            if (!access.HasUnit(parsed)) return Failure(403, "FORBIDDEN", "Unidade fora do seu acesso.");
            selectedUnit = parsed;
        }
        var units = await db.Units.AsNoTracking().Where(unit => unit.Active && unit.DeletedAt == null
            && allowedCompanies.Contains(unit.CompanyId) && access.UnitIds.Contains(unit.Id)
            && (!selectedUnit.HasValue || unit.Id == selectedUnit.Value)).OrderBy(unit => unit.Name).ToListAsync(cancellationToken);
        if (selectedUnit.HasValue && units.Count == 0) return Failure(403, "FORBIDDEN", "Unidade fora do seu acesso.");
        var companyIds = selectedUnit.HasValue ? units.Select(unit => unit.CompanyId).Distinct().ToArray() : allowedCompanies;
        var unitIds = units.Select(unit => unit.Id).ToArray();
        var companies = await db.Companies.AsNoTracking().Where(company => company.Active && company.DeletedAt == null
            && companyIds.Contains(company.Id)).OrderBy(company => company.FantasyName).ToListAsync(cancellationToken);
        var sectors = await db.Departments.AsNoTracking().Where(sector => sector.Active && sector.DeletedAt == null
            && access.SectorIds.Contains(sector.Id) && unitIds.Contains(sector.UnitId)).OrderBy(sector => sector.Name).ToListAsync(cancellationToken);
        var jobs = await db.Positions.AsNoTracking().Where(job => job.Active && job.DeletedAt == null && companyIds.Contains(job.CompanyId))
            .OrderBy(job => job.Name).ToListAsync(cancellationToken);
        return Ok(new BrowserOrganizationResponse(
            companies.Select(company => new BrowserOrganizationItem(company.Id, company.FantasyName, "ACTIVE",
                TaxId: company.Cnpj.Value, Classification: company.TipoEmpresa == CompanyType.INTERNA ? "INTERNAL" : "SERVICE_PROVIDER")).ToArray(),
            Array.Empty<BrowserOrganizationItem>(),
            units.Select(unit => new BrowserOrganizationItem(unit.Id, unit.Name, "ACTIVE", Code: unit.Code, CompanyId: unit.CompanyId)).ToArray(),
            sectors.Select(sector => new BrowserOrganizationItem(sector.Id, sector.Name, "ACTIVE", UnitId: sector.UnitId,
                Description: sector.Description, ResponsibleIds: sector.ResponsibleId.HasValue ? [sector.ResponsibleId.Value] : [])).ToArray(),
            jobs.Select(job => new BrowserOrganizationItem(job.Id, job.Name, "ACTIVE", CompanyId: job.CompanyId,
                Description: job.Description, OccupationCode: job.CBO)).ToArray(),
            Array.Empty<BrowserOrganizationItem>(), Array.Empty<BrowserOrganizationItem>()));
    }

    private ObjectResult Failure(int status, string code, string message) => StatusCode(status, new BrowserErrorResponse(code, message, HttpContext.TraceIdentifier));
}
