using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record BrowserIdentity(UserAuthenticationData Authentication, Guid? WorkerId, bool AmbiguousWorker, BrowserWorkspaceAccess? Workspace = null);

public interface IBrowserIdentityReader
{
    Task<BrowserIdentity?> FindAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class BrowserIdentityReader(
    IUserAuthenticationRepository authentication,
    SesmtDbContext db,
    IBrowserWorkspaceAccessReader workspace) : IBrowserIdentityReader
{
    public async Task<BrowserIdentity?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var data = await authentication.GetByIdAsync(userId, cancellationToken);
        if (data is null || !data.User.Active || data.User.DeletedAt is not null) return null;

        // More than one active link needs an explicit company context; never choose an arbitrary worker.
        var workers = await db.Workers.AsNoTracking()
            .Where(worker => worker.UserId == userId && worker.Active && worker.DeletedAt == null)
            .Select(worker => worker.Id).Take(2).ToListAsync(cancellationToken);
        return new BrowserIdentity(data, workers.Count == 1 ? workers[0] : null, workers.Count > 1,
            await workspace.ReadAsync(userId, cancellationToken));
    }
}
