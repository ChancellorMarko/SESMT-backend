using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.API.Services;
using SESMT.Application.Exceptions;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

[ApiController, Authorize, Route("organization-management")]
public sealed class OrganizationManagementController(NativeResourceAccess access, SesmtDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var snapshot = await access.ReadAsync(ct);
        if (snapshot.OwnOnly) throw new ResourceForbiddenException();
        var readable = await (from a in db.UserRoles join r in db.Roles on a.RoleId equals r.Id
            join rp in db.RolePermissions on r.Id equals rp.RoleId join p in db.Permissions on rp.PermissionId equals p.Id
            where a.UserId == snapshot.UserId && a.Active && a.DeletedAt == null && r.Active && r.DeletedAt == null
                && rp.Active && rp.DeletedAt == null && p.Active && p.DeletedAt == null && p.Action == "ler"
                && (p.Resource == "empresa" || p.Resource == "unidade" || p.Resource == "setor" || p.Resource == "cargo")
            select a.Id).AnyAsync(ct);
        if (!readable) throw new ResourceForbiddenException();
        var companyIds = NativeResourceAccess.Companies(snapshot, "empresa:ler");
        var companies = await db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id) && c.Active && c.DeletedAt == null)
            .OrderBy(c => c.FantasyName).Select(c => new { c.Id, Name = c.FantasyName }).ToListAsync(ct);
        var unitCompanies = NativeResourceAccess.Companies(snapshot, "unidade:ler");
        var units = await db.Units.AsNoTracking().Where(u => unitCompanies.Contains(u.CompanyId) && snapshot.UnitIds.Contains(u.Id) && u.Active && u.DeletedAt == null)
            .OrderBy(u => u.Name).Select(u => new { u.Id, u.Name, u.Code, u.CompanyId }).ToListAsync(ct);
        var sectorCompanies = NativeResourceAccess.Companies(snapshot, "setor:ler");
        var sectors = await (from d in db.Departments.AsNoTracking() join u in db.Units on d.UnitId equals u.Id
            where sectorCompanies.Contains(u.CompanyId) && snapshot.SectorIds.Contains(d.Id) && d.Active && d.DeletedAt == null
                && u.Active && u.DeletedAt == null
            orderby d.Name select new { d.Id, d.Name, d.UnitId }).ToListAsync(ct);
        var positionCompanies = NativeResourceAccess.Companies(snapshot, "cargo:ler");
        var positions = await db.Positions.AsNoTracking().Where(p => positionCompanies.Contains(p.CompanyId) && p.Active && p.DeletedAt == null)
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name, p.CompanyId }).ToListAsync(ct);
        var canCreateCompany = true;
        try { await access.RequireGlobalCompanyCreationAsync(ct); }
        catch (ResourceForbiddenException) { canCreateCompany = false; }
        return Ok(new
        {
            canCreateCompany,
            companies = companies.Select(c => new { c.Id, c.Name,
                canCreateUnit = snapshot.IsFullCompany(c.Id) && snapshot.Can("unidade:criar", c.Id),
                canCreatePosition = snapshot.IsFullCompany(c.Id) && snapshot.Can("cargo:criar", c.Id) }),
            units = units.Select(u => new { u.Id, u.Name, u.Code, u.CompanyId,
                canCreateDepartment = snapshot.FullUnitIds.Contains(u.Id) && snapshot.Can("setor:criar", u.CompanyId) }),
            departments = sectors, positions
        });
    }
}
