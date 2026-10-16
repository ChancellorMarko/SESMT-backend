using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Domain.Modules.Workforce.Repositories;

public interface IWorkerRepository
{
    Task<Worker?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retorna os trabalhadores ativos cujos IDs estão na lista informada.
    /// </summary>
    Task<IReadOnlyList<Worker>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default);

    /// <summary>
    /// Verifica se o trabalhador existe, está ativo e pertence à empresa informada.
    /// </summary>
    Task<bool> ExistsInCompanyAsync(
        Guid workerId,
        Guid companyId,
        CancellationToken ct = default);
}