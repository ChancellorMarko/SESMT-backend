using SESMT.Application.Modules.IdentityAccess.DTOs;

namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

public interface IPasswordResetService
{
    Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<PasswordResetResult> ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
