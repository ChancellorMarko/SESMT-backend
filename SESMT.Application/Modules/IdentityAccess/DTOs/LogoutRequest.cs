namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa a solicitação de encerramento de sessão.
/// </summary>
public sealed record LogoutRequest(
    string RefreshToken);
