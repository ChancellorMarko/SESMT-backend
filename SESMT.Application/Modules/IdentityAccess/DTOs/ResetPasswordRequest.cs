namespace SESMT.Application.Modules.IdentityAccess.DTOs;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
