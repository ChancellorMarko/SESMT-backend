namespace SESMT.Application.Modules.IdentityAccess.DTOs;

public sealed record PasswordResetResult(bool Succeeded, string? ErrorCode = null)
{
    public static PasswordResetResult Success() => new(true);
    public static PasswordResetResult Failure(string errorCode) => new(false, errorCode);
}
