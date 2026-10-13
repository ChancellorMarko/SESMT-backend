namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa o resultado de uma tentativa de logout.
/// </summary>
public sealed record LogoutResult(
    bool Succeeded,
    string? ErrorCode)
{
    public static LogoutResult Success() =>
        new(true, null);

    public static LogoutResult Failure(string errorCode) =>
        new(false, errorCode);
}
