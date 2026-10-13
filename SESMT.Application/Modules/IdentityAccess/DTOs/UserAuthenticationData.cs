using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Contém os dados necessários para autenticar um usuário.
/// </summary>
public sealed record UserAuthenticationData(
    User User,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<UserScopeClaim> Scopes);