using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Domain.Modules.Organization.Repositories;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Team>> ListByLocationAsync(
        Guid locationId,
        int skip,
        int take,
        CancellationToken ct = default);

    Task AddAsync(Team team, CancellationToken ct = default);

    void Update(Team team);
}