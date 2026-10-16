using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Repositories;

namespace SESMT.Infrastructure.Persistence.Repositories.Organization;

public class TeamRepository : ITeamRepository
{
    private readonly SesmtDbContext _db;

    public TeamRepository(SesmtDbContext db) => _db = db;

    public Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Team>> ListByLocationAsync(
        Guid locationId, int skip, int take, CancellationToken ct = default)
        => await _db.Teams
            .Where(t => (t.LocalId == locationId || t.OrganizationId == locationId)
                        && t.Active
                        && t.DeletedAt == null)
            .OrderBy(t => t.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(Team team, CancellationToken ct = default)
        => await _db.Teams.AddAsync(team, ct);

    public void Update(Team team) => _db.Teams.Update(team);
}
