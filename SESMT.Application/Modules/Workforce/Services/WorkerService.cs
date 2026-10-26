using SESMT.Application.Common;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Application.Modules.Workforce.Interfaces;
using SESMT.Application.Modules.Workforce.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Audit.Constants;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Audit.Repositories;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.Workforce.Repositories;
using SESMT.Domain.ValueObjects;

namespace SESMT.Application.Modules.Workforce.Services;

public sealed class WorkerService : IWorkerService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkerRepository _repository;
    private readonly IWorkerHistoryRepository _historyRepository;
    private readonly ICpfProtectionService _cpfProtection;
    private readonly IAuditLogRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkerService(
        IWorkerRepository repository,
        IWorkerHistoryRepository historyRepository,
        ICpfProtectionService cpfProtection,
        IUnitOfWork unitOfWork,
        IAuditLogRepository auditRepository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _historyRepository = historyRepository;
        _cpfProtection = cpfProtection;
        _unitOfWork = unitOfWork;
        _auditRepository = auditRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<WorkerResponse>> CreateAsync(
        WorkerCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Valida CPF via VO
        Cpf cpf;
        try
        {
            cpf = new Cpf(request.Cpf);
        }
        catch (ArgumentException ex)
        {
            return Result<WorkerResponse>.Failure(ex.Message);
        }

        var (encrypted, hash) = _cpfProtection.Protect(cpf.Value);

        // 2. Matrícula única
        if (await _repository.ExistsByEnrollmentAsync(
                request.CompanyId, request.Enrollment, null, cancellationToken))
        {
            return Result<WorkerResponse>.Failure(
                "Já existe um trabalhador com essa matrícula nesta empresa.");
        }

        // 3. CPF único
        if (await _repository.ExistsByCpfHashAsync(
                request.CompanyId, hash, null, cancellationToken))
        {
            return Result<WorkerResponse>.Failure(
                "Já existe um trabalhador com esse CPF nesta empresa.");
        }

        try
        {
            var worker = WorkerMapper.ToEntity(request, encrypted, hash);

            await _repository.AddAsync(worker, cancellationToken);

            if (request.PositionId.HasValue && request.DepartmentId.HasValue)
            {
                var initialHistory = new WorkerHistory(
                    workerId: worker.Id,
                    departmentId: request.DepartmentId.Value,
                    positionId: request.PositionId.Value,
                    startDate: request.AdmissionDate,
                    reasonForChange: "Admissão");

                await _historyRepository.AddAsync(initialHistory, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<WorkerResponse>.Success(
                WorkerMapper.ToResponse(worker, cpf.ToMaskedString()));
        }
        catch (ArgumentException ex)
        {
            return Result<WorkerResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<WorkerResponse>> GetByIdAsync(
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        var worker = await _repository.GetByIdAsync(workerId, cancellationToken);
        if (worker is null)
            return Result<WorkerResponse>.Failure("Trabalhador não encontrado.");

        var rawCpf = _cpfProtection.Unprotect(worker.CpfEncryption);
        var cpf = new Cpf(rawCpf);

        // ─── Audit: leitura de dado sensível ────────────────────────────────
        var lastHash = await _auditRepository.GetLastHashAsync(cancellationToken);
        await _auditRepository.AddAsync(
            AuditLog.ForSensitiveView(
                resourceType: AuditResource.Worker,
                resourceId: worker.Id,
                actorId: _currentUser.UserId,
                actorEmail: _currentUser.Email,
                actorName: _currentUser.Name,
                previousHash: lastHash,
                ipAddress: _currentUser.IpAddress,
                userAgent: _currentUser.UserAgent,
                companyId: worker.CompanyId,
                traceId: _currentUser.TraceId),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<WorkerResponse>.Success(
            WorkerMapper.ToResponse(worker, cpf.ToMaskedString()));
    }

    public async Task<Result<IEnumerable<WorkerSummaryResponse>>> ListAsync(
        Guid companyId,
        Guid? unitId = null,
        Guid? departmentId = null,
        Guid? positionId = null,
        string? search = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var skip = (pageNumber - 1) * pageSize;

        var workers = await _repository.ListByCompanyAsync(
            companyId, unitId, departmentId, positionId, search,
            skip, pageSize, cancellationToken);

        var dtos = workers.Select(WorkerMapper.ToSummary).ToList();

        return Result<IEnumerable<WorkerSummaryResponse>>.Success(dtos);
    }

    public async Task<Result<WorkerResponse>> UpdateAsync(
        Guid workerId,
        WorkerUpdateRequest updateData,
        CancellationToken cancellationToken = default)
    {
        var worker = await _repository.GetByIdAsync(workerId, cancellationToken);
        if (worker is null)
            return Result<WorkerResponse>.Failure("Trabalhador não encontrado.");

        // Matrícula única
        if (await _repository.ExistsByEnrollmentAsync(
                worker.CompanyId, updateData.Enrollment, workerId, cancellationToken))
        {
            return Result<WorkerResponse>.Failure(
                "Já existe um trabalhador com essa matrícula nesta empresa.");
        }

        // Detectar mudança de alocação
        var allocationChanged =
            worker.PositionId != updateData.PositionId ||
            worker.DepartmentId != updateData.DepartmentId ||
            worker.UnitId != updateData.UnitId;

        try
        {
            worker.Update(
                enrollment: updateData.Enrollment,
                name: updateData.Name,
                dateOfBirth: updateData.DateOfBirth,
                admissionDate: updateData.AdmissionDate,
                dismissalDate: updateData.DismissalDate,
                positionId: updateData.PositionId,
                unitId: updateData.UnitId,
                departmentId: updateData.DepartmentId);

            if (allocationChanged
                && updateData.PositionId.HasValue
                && updateData.DepartmentId.HasValue)
            {
                await HandleHistoryChange(
                    worker,
                    updateData.PositionId.Value,
                    updateData.DepartmentId.Value,
                    updateData.ChangeReason,
                    cancellationToken);
            }

            _repository.Update(worker);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var rawCpf = _cpfProtection.Unprotect(worker.CpfEncryption);
            var maskedCpf = new Cpf(rawCpf).ToMaskedString();

            return Result<WorkerResponse>.Success(
                WorkerMapper.ToResponse(worker, maskedCpf));
        }
        catch (ArgumentException ex)
        {
            return Result<WorkerResponse>.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<WorkerResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeactivateAsync(
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        var worker = await _repository.GetByIdAsync(workerId, cancellationToken);
        if (worker is null)
            return Result<bool>.Failure("Trabalhador não encontrado.");

        worker.Deactivate();
        _repository.Update(worker);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task HandleHistoryChange(
        Worker worker,
        Guid newPositionId,
        Guid newDepartmentId,
        string? reason,
        CancellationToken ct)
    {
        var activeHistory = await _historyRepository.GetActiveByWorkerAsync(worker.Id, ct);

        if (activeHistory is not null)
        {
            activeHistory.Close(DateOnly.FromDateTime(DateTime.UtcNow), reason);
            _historyRepository.Update(activeHistory);
        }

        var newHistory = new WorkerHistory(
            workerId: worker.Id,
            departmentId: newDepartmentId,
            positionId: newPositionId,
            startDate: DateOnly.FromDateTime(DateTime.UtcNow),
            endDate: null,
            reasonForChange: reason ?? "Mudança de função/setor");

        await _historyRepository.AddAsync(newHistory, ct);
    }
}
