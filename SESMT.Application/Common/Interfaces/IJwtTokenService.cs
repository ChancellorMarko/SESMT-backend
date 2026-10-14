using SESMT.Application.Modules.IdentityAccess.DTOs;

namespace SESMT.Application.Common.Interfaces;

/// <summary>
/// Define o contrato para geração de tokens JWT.
/// </summary>
public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(
        Guid userId,
        IEnumerable<string> roles,
        IEnumerable<UserScopeClaim> scopes);
}