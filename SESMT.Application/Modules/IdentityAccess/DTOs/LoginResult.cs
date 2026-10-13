namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa o resultado de uma tentativa de login.
/// </summary>
public sealed record LoginResult(
    bool Succeeded,
    LoginResponse? Response,
    string? ErrorCode)
{
    public static LoginResult Success(LoginResponse response) =>
        new(true, response, null);

    public static LoginResult Failure(string errorCode) =>
        new(false, null, errorCode);
}