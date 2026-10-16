using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.Interfaces;

public interface IWorkPermitReadinessService
{
    Task<bool> IsReadyAsync(WorkPermit permit, CancellationToken cancellationToken = default);
}
