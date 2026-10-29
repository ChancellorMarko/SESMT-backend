using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.WorkPermits;

public sealed class FailClosedWorkPermitReadinessService : IWorkPermitReadinessService
{
    private readonly SESMT.Application.Modules.WorkPermits.Interfaces.IWorkPermitRepository _repository;

    public FailClosedWorkPermitReadinessService(SESMT.Application.Modules.WorkPermits.Interfaces.IWorkPermitRepository repository) =>
        _repository = repository;

    public Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default) =>
        _repository.IsReadyAsync(permit, cancellationToken);

    public Task<WorkPermitReadinessAssessment> EvaluateAsync(WorkPermit permit, CancellationToken cancellationToken = default) =>
        _repository.EvaluateReadinessAsync(permit, cancellationToken);
}
