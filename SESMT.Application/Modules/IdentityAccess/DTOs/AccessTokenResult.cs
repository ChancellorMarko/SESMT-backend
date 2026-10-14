namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Contém o token de acesso e seu instante real de expiração.
/// </summary>
public sealed record AccessTokenResult(
    string Token,
    DateTime ExpiresAtUtc);