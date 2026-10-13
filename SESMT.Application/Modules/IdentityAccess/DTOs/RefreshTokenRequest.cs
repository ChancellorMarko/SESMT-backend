namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa a solicitação de renovação da sessão.
/// </summary>
public sealed record RefreshTokenRequest(
    string RefreshToken);
