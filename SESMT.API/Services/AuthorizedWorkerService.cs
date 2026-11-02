using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Application.Modules.Workforce.Interfaces;
using SESMT.Application.Modules.Workforce.Mappers;
using SESMT.Application.Modules.Workforce.Services;
using SESMT.Domain.Common.Result;

namespace SESMT.API.Services;

public sealed class AuthorizedWorkerService(WorkerService core, NativeResourceAccess access) : IWorkerService
{
    public async Task<Result<WorkerResponse>> CreateAsync(WorkerCreateRequest request, CancellationToken cancellationToken = default)
    {
        await access.ValidateWorkerContextAsync("criar", request.CompanyId, request.UnitId, request.DepartmentId, request.PositionId, cancellationToken);
        await access.ValidateAccountAsync(request.UserId, request.CompanyId, null, cancellationToken);
        return await core.CreateAsync(request, cancellationToken);
    }

    public async Task<Result<WorkerResponse>> GetByIdAsync(Guid workerId, CancellationToken cancellationToken = default)
    {
        await access.WorkerAsync(workerId, "ler", cancellationToken);
        // The sensitive-view audit and CPF decryption happen only inside this authorized call.
        return await core.GetByIdAsync(workerId, cancellationToken);
    }

    public async Task<Result<IEnumerable<WorkerSummaryResponse>>> ListAsync(Guid companyId, Guid? unitId = null,
        Guid? departmentId = null, Guid? positionId = null, string? search = null, int pageSize = 10,
        int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync("trabalhador:ler", companyId, false, cancellationToken);
        var (skip, take) = NativeResourceAccess.Page(pageSize, pageNumber);
        var query = (await access.WorkersAsync("ler", cancellationToken)).AsNoTracking().Where(w => w.CompanyId == companyId);
        if (unitId.HasValue) query = query.Where(w => w.UnitId == unitId);
        if (departmentId.HasValue) query = query.Where(w => w.DepartmentId == departmentId);
        if (positionId.HasValue) query = query.Where(w => w.PositionId == positionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(w => w.Name.Value.ToLower().Contains(term) || w.Enrollment.ToLower().Contains(term));
        }
        var workers = await query.OrderBy(w => w.Name.Value).ThenBy(w => w.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return Result<IEnumerable<WorkerSummaryResponse>>.Success(workers.Select(WorkerMapper.ToSummary).ToArray());
    }

    public async Task<Result<WorkerResponse>> UpdateAsync(Guid workerId, WorkerUpdateRequest updateData, CancellationToken cancellationToken = default)
    {
        var worker = await access.WorkerAsync(workerId, "editar", cancellationToken);
        await access.ValidateWorkerContextAsync("editar", worker.CompanyId, updateData.UnitId, updateData.DepartmentId, updateData.PositionId, cancellationToken);
        return await core.UpdateAsync(workerId, updateData, cancellationToken);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid workerId, CancellationToken cancellationToken = default)
    {
        await access.WorkerAsync(workerId, "excluir", cancellationToken);
        return await core.DeactivateAsync(workerId, cancellationToken);
    }
}

public sealed class AuthorizedWorkerHistoryService(WorkerHistoryService core, NativeResourceAccess access) : IWorkerHistoryService
{
    public async Task<Result<IEnumerable<WorkerHistoryResponse>>> ListByWorkerAsync(Guid workerId, CancellationToken cancellationToken = default)
    {
        await access.WorkerAsync(workerId, "ler", cancellationToken);
        return await core.ListByWorkerAsync(workerId, cancellationToken);
    }

    public async Task<Result<WorkerHistoryResponse>> AddManualAsync(Guid workerId, WorkerHistoryCreateRequest request, CancellationToken cancellationToken = default)
    {
        var worker = await access.WorkerAsync(workerId, "editar", cancellationToken);
        await access.ValidateHistoryAsync(worker, request.DepartmentId, request.PositionId, cancellationToken);
        return await core.AddManualAsync(workerId, request, cancellationToken);
    }
}
