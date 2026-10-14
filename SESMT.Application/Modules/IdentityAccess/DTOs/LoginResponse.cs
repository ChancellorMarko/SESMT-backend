namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa o resultado de uma autenticação bem-sucedida.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<UserScopeClaim> Scopes);
