using SESMT.Application.Modules.IdentityAccess.DTOs;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

/// <summary>
/// Define o caso de uso de encerramento de sessão.
/// </summary>
public interface ILogoutService
{
    Task<LogoutResult> ExecuteAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);
}
