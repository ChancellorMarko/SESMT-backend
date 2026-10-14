using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;

/// <summary>
/// Implementa as consultas necessárias para autenticar usuários.
/// </summary>
public sealed class UserAuthenticationRepository : IUserAuthenticationRepository
{
    private readonly SesmtDbContext _dbContext;

    public UserAuthenticationRepository(SesmtDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserAuthenticationData?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Email == normalizedEmail && user.DeletedAt == null,
                cancellationToken);

        if (user is null)
            return null;

        return await CreateAuthenticationDataAsync(
            user,
            cancellationToken);
    }

    public async Task<UserAuthenticationData?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "O ID do usuário é obrigatório.",
                nameof(userId));
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Id == userId && user.DeletedAt == null,
                cancellationToken);

        if (user is null)
            return null;

        return await CreateAuthenticationDataAsync(
            user,
            cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserAuthenticationData> CreateAuthenticationDataAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var roles = await (
            from userRole in _dbContext.UserRoles
            join role in _dbContext.Roles
                on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
                && userRole.Active
                && userRole.DeletedAt == null
                && role.Active
                && role.DeletedAt == null
            select role.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var scopes = await _dbContext.UserScopes
            .Where(userScope =>
                userScope.UserId == user.Id
                && userScope.Active
                && userScope.DeletedAt == null)
            .Select(userScope => new UserScopeClaim(
                userScope.ScopeType,
                userScope.ReferenceId))
            .ToListAsync(cancellationToken);

        return new UserAuthenticationData(user, roles, scopes);
    }
}
