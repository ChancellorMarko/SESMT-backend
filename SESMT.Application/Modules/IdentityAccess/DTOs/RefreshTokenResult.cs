namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa o resultado de uma tentativa de renovação da sessão.
/// </summary>
public sealed record RefreshTokenResult(
    bool Succeeded,
    LoginResponse? Response,
    string? ErrorCode)
{
    public static RefreshTokenResult Success(LoginResponse response) =>
        new(true, response, null);

    public static RefreshTokenResult Failure(string errorCode) =>
        new(false, null, errorCode);
}
