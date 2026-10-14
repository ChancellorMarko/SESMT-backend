using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

public interface IPasswordResetRepository
{
    Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
