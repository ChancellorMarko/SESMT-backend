namespace SESMT.Application.Modules.IdentityAccess.Interfaces;

public interface IPasswordResetNotificationService
{
    Task SendAsync(string email, string token, CancellationToken cancellationToken = default);
}
