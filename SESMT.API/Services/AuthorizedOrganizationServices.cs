using Microsoft.EntityFrameworkCore;
using SESMT.Application.Exceptions;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Mappers;
using SESMT.Application.Modules.Organization.Services;
using SESMT.Domain.Common.Result;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed class AuthorizedCompanyService(CompanyService core, NativeResourceAccess access, SesmtDbContext db) : ICompanyService
{
    public async Task<Result<CompanyResponse>> CreateAsync(CompanyCreateRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireGlobalCompanyCreationAsync(cancellationToken);
        return await core.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<CompanyResponse>> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("empresa:ler", companyId, false, cancellationToken);
        return await core.GetByIdAsync(companyId, cancellationToken);
    }

    public async Task<Result<IEnumerable<CompanySummaryResponse>>> ListAsync(int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var allowed = NativeResourceAccess.Companies(await access.ReadAsync(cancellationToken), "empresa:ler");
        var (skip, take) = NativeResourceAccess.Page(pageSize, pageNumber);
        var rows = await db.Companies.AsNoTracking().Where(c => c.Active && c.DeletedAt == null && allowed.Contains(c.Id))
            .OrderBy(c => c.CorporateName).ThenBy(c => c.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return Result<IEnumerable<CompanySummaryResponse>>.Success(rows.Select(CompanyMapper.ToSummary).ToArray());
    }

    public async Task<Result<CompanyResponse>> UpdateAsync(Guid companyId, CompanyCreateRequest updateData, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("empresa:editar", companyId, true, cancellationToken);
        return await core.UpdateAsync(companyId, updateData, cancellationToken);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("empresa:excluir", companyId, true, cancellationToken);
        return await core.DeactivateAsync(companyId, cancellationToken);
    }
}

public sealed class AuthorizedUnitService(UnitService core, NativeResourceAccess access, SesmtDbContext db) : IUnitService
{
    public async Task<Result<UnitResponse>> CreateAsync(UnitCreateRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("unidade:criar", request.CompanyId, true, cancellationToken);
        return await core.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<UnitResponse>> GetByIdAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        await access.UnitAsync(unitId, "unidade", "ler", cancellationToken);
        return await core.GetByIdAsync(unitId, cancellationToken);
    }

    public async Task<Result<IEnumerable<UnitSummaryResponse>>> ListByCompanyAsync(Guid companyId, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("unidade:ler", companyId, false, cancellationToken);
        return await ListAsync(companyId, null, pageSize, pageNumber, cancellationToken);
    }

    public async Task<Result<IEnumerable<UnitSummaryResponse>>> ListAsync(Guid? companyId = null, string? code = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var snapshot = await access.ReadAsync(cancellationToken);
        var companies = NativeResourceAccess.Companies(snapshot, "unidade:ler");
        var units = snapshot.UnitIds.ToArray();
        var (skip, take) = NativeResourceAccess.Page(pageSize, pageNumber);
        var query = db.Units.AsNoTracking().Where(u => companies.Contains(u.CompanyId) && units.Contains(u.Id) && u.Active && u.DeletedAt == null);
        if (companyId.HasValue) query = query.Where(u => u.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var term = code.Trim().ToLowerInvariant();
            query = query.Where(u => u.Code.ToLower().Contains(term));
        }
        var rows = await query.OrderBy(u => u.Name).ThenBy(u => u.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return Result<IEnumerable<UnitSummaryResponse>>.Success(rows.Select(UnitMapper.ToSummary).ToArray());
    }

    public async Task<Result<UnitResponse>> UpdateAsync(Guid unitId, UnitUpdateRequest updateData, CancellationToken cancellationToken = default)
    {
        await access.UnitAsync(unitId, "unidade", "editar", cancellationToken);
        return await core.UpdateAsync(unitId, updateData, cancellationToken);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        await access.UnitAsync(unitId, "unidade", "excluir", cancellationToken);
        return await core.DeactivateAsync(unitId, cancellationToken);
    }
}

public sealed class AuthorizedDepartmentService(DepartmentService core, NativeResourceAccess access, SesmtDbContext db) : IDepartmentService
{
    public async Task<Result<DepartmentResponse>> CreateAsync(DepartmentCreateRequest request, CancellationToken cancellationToken = default)
    {
        await access.UnitAsync(request.UnitId, "setor", "criar", cancellationToken);
        return await core.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<DepartmentResponse>> GetByIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        await access.DepartmentAsync(departmentId, "ler", cancellationToken);
        return await core.GetByIdAsync(departmentId, cancellationToken);
    }

    public async Task<Result<IEnumerable<DepartmentSummaryResponse>>> ListByUnitAsync(Guid unitId, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await access.UnitAsync(unitId, "setor", "ler", cancellationToken);
        var sectors = (await access.ReadAsync(cancellationToken)).SectorIds.ToArray();
        var (skip, take) = NativeResourceAccess.Page(pageSize, pageNumber);
        var rows = await db.Departments.AsNoTracking().Where(d => d.UnitId == unitId && sectors.Contains(d.Id) && d.Active && d.DeletedAt == null)
            .OrderBy(d => d.Name).ThenBy(d => d.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return Result<IEnumerable<DepartmentSummaryResponse>>.Success(rows.Select(DepartmentMapper.ToSummary).ToArray());
    }

    public async Task<Result<DepartmentResponse>> UpdateAsync(Guid departmentId, DepartmentUpdateRequest updateData, CancellationToken cancellationToken = default)
    {
        await access.DepartmentAsync(departmentId, "editar", cancellationToken);
        return await core.UpdateAsync(departmentId, updateData, cancellationToken);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        await access.DepartmentAsync(departmentId, "excluir", cancellationToken);
        return await core.DeactivateAsync(departmentId, cancellationToken);
    }
}

public sealed class AuthorizedPositionService(PositionService core, NativeResourceAccess access, SesmtDbContext db) : IPositionService
{
    public async Task<Result<PositionResponse>> CreateAsync(PositionCreateRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("cargo:criar", request.CompanyId, true, cancellationToken);
        return await core.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<PositionResponse>> GetByIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        await access.PositionAsync(positionId, "ler", cancellationToken);
        return await core.GetByIdAsync(positionId, cancellationToken);
    }

    public async Task<Result<IEnumerable<PositionSummaryResponse>>> ListByCompanyAsync(Guid companyId, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("cargo:ler", companyId, false, cancellationToken);
        var (skip, take) = NativeResourceAccess.Page(pageSize, pageNumber);
        var rows = await db.Positions.AsNoTracking().Where(p => p.CompanyId == companyId && p.Active && p.DeletedAt == null)
            .OrderBy(p => p.Name).ThenBy(p => p.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return Result<IEnumerable<PositionSummaryResponse>>.Success(rows.Select(PositionMapper.ToSummary).ToArray());
    }

    public async Task<Result<PositionResponse>> UpdateAsync(Guid positionId, PositionUpdateRequest updateData, CancellationToken cancellationToken = default)
    {
        await access.PositionAsync(positionId, "editar", cancellationToken);
        return await core.UpdateAsync(positionId, updateData, cancellationToken);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        await access.PositionAsync(positionId, "excluir", cancellationToken);
        return await core.DeactivateAsync(positionId, cancellationToken);
    }
}

public sealed class AuthorizedTeamService(TeamService core, NativeResourceAccess access) : ITeamService
{
    public async Task<Result<TeamResponse>> CreateAsync(TeamCreateRequest request, CancellationToken cancellationToken = default)
    {
        await access.ValidateTeamAsync("criar", request.CompanyId, request.LocalId, request.ChiefId, request.ParticipantIds, cancellationToken);
        return await core.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<TeamResponse>> GetByIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await access.TeamAsync(teamId, "ler", cancellationToken);
        return await core.GetByIdAsync(teamId, cancellationToken);
    }

    public async Task<Result<IEnumerable<TeamSummaryResponse>>> ListByLocationAsync(Guid locationId, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await access.UnitAsync(locationId, "equipe", "ler", cancellationToken);
        var (skip, take) = NativeResourceAccess.Page(pageSize, pageNumber);
        var rows = await (await access.TeamsAsync("ler", cancellationToken)).AsNoTracking().Where(t => t.LocalId == locationId)
            .OrderBy(t => t.Name).ThenBy(t => t.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return Result<IEnumerable<TeamSummaryResponse>>.Success(rows.Select(TeamMapper.ToSummary).ToArray());
    }

    public async Task<Result<TeamResponse>> UpdateAsync(Guid teamId, TeamPatchRequest patchData, CancellationToken cancellationToken = default)
    {
        var team = await access.TeamAsync(teamId, "editar", cancellationToken);
        await access.ValidateTeamAsync("editar", team.CompanyId, team.LocalId, patchData.ChiefId ?? team.ChiefId,
            patchData.ParticipantIds ?? team.ParticipantIds, cancellationToken);
        return await core.UpdateAsync(teamId, patchData, cancellationToken);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await access.TeamAsync(teamId, "excluir", cancellationToken);
        return await core.DeactivateAsync(teamId, cancellationToken);
    }
}
