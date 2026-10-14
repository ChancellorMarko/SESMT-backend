using SESMT.Application.Modules.IdentityAccess.DTOs;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

/// <summary>
/// Define o caso de uso de renovação de sessão.
/// </summary>
public interface IRefreshSessionService
{
    Task<RefreshTokenResult> ExecuteAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
