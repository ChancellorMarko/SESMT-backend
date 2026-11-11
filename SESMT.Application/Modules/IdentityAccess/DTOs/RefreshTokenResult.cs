namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa o resultado de uma tentativa de renovação da sessão.
/// </summary>
public sealed record RefreshTokenResult(
    bool Succeeded,
    LoginResponse? Response,
    string? ErrorCode)
{
    /// <summary>Another refresh rotated the same token first; the session stays valid.</summary>
    public const string ConflictError = "refresh_conflict";

    public static RefreshTokenResult Success(LoginResponse response) =>
        new(true, response, null);

    public static RefreshTokenResult Failure(string errorCode) =>
        new(false, null, errorCode);
}
