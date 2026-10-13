using SESMT.Application.Modules.IdentityAccess.DTOs;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

/// <summary>
/// Define o caso de uso de autenticação de usuários.
/// </summary>
public interface ILoginService
{
    Task<LoginResult> ExecuteAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}