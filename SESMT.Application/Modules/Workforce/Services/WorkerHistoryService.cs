using SESMT.Application.Common;
using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Application.Modules.Workforce.Interfaces;
using SESMT.Application.Modules.Workforce.Mappers;
using SESMT.Domain.Common.Result;
using SESMT.Domain.Modules.Workforce.Repositories;

namespace SESMT.Application.Modules.Workforce.Services;

public sealed class WorkerHistoryService : IWorkerHistoryService
{
    private readonly IWorkerHistoryRepository _historyRepository;
    private readonly IWorkerRepository _workerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkerHistoryService(
        IWorkerHistoryRepository historyRepository,
        IWorkerRepository workerRepository,
        IUnitOfWork unitOfWork)
    {
        _historyRepository = historyRepository;
        _workerRepository = workerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IEnumerable<WorkerHistoryResponse>>> ListByWorkerAsync(
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        var worker = await _workerRepository.GetByIdAsync(workerId, cancellationToken);
        if (worker is null)
            return Result<IEnumerable<WorkerHistoryResponse>>.Failure(
                "Trabalhador não encontrado.");

        var history = await _historyRepository.ListByWorkerAsync(workerId, cancellationToken);
        var dtos = history.Select(WorkerHistoryMapper.ToResponse).ToList();

        return Result<IEnumerable<WorkerHistoryResponse>>.Success(dtos);
    }

    public async Task<Result<WorkerHistoryResponse>> AddManualAsync(
        Guid workerId,
        WorkerHistoryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var worker = await _workerRepository.GetByIdAsync(workerId, cancellationToken);
        if (worker is null)
            return Result<WorkerHistoryResponse>.Failure("Trabalhador não encontrado.");

        try
        {
            var history = WorkerHistoryMapper.ToEntity(workerId, request);

            await _historyRepository.AddAsync(history, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<WorkerHistoryResponse>.Success(
                WorkerHistoryMapper.ToResponse(history));
        }
        catch (ArgumentException ex)
        {
            return Result<WorkerHistoryResponse>.Failure(ex.Message);
        }
    }
}
