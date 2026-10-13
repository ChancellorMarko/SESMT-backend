using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;

public sealed class PasswordResetRepository : IPasswordResetRepository
{
    private readonly SesmtDbContext _dbContext;

    public PasswordResetRepository(SesmtDbContext dbContext) => _dbContext = dbContext;

    public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return _dbContext.Users.FirstOrDefaultAsync(
            user => user.Email == normalizedEmail && user.Active && user.DeletedAt == null,
            cancellationToken);
    }

    public Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == userId && user.Active && user.DeletedAt == null,
            cancellationToken);

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
        await _dbContext.PasswordResetTokens.AddAsync(token, cancellationToken);

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _dbContext.PasswordResetTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash && token.Active && token.DeletedAt == null,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
