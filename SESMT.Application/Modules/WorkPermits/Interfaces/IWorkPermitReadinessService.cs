using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.Interfaces;

public interface IWorkPermitReadinessService
{
    Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default);
    async Task<WorkPermitReadinessAssessment> EvaluateAsync(WorkPermit permit, CancellationToken cancellationToken = default)
    {
        var ready = await IsReadyAsync(permit, cancellationToken);
        return new(ready, ready ? [] : [new("VERIFICACAO_PENDENTE")]);
    }
}
