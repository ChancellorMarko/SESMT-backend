using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SESMT.API.Configuration;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.API.Services;

public sealed class SmtpPasswordResetNotificationService : IPasswordResetNotificationService
{
    private readonly PasswordResetEmailSettings _settings;
    private readonly ILogger<SmtpPasswordResetNotificationService> _logger;

    public SmtpPasswordResetNotificationService(
        IOptions<PasswordResetEmailSettings> settings,
        ILogger<SmtpPasswordResetNotificationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.From))
        {
            _logger.LogError("Envio de recuperação de senha não configurado.");
            return;
        }

        try
        {
            using var message = new MailMessage(_settings.From, email)
            {
                Subject = "Redefinição de senha SESMT",
                Body = $"Use este token para redefinir sua senha. Ele expira em 30 minutos:\n\n{token}",
                IsBodyHtml = false
            };
            var password = string.IsNullOrWhiteSpace(_settings.Password)
                ? null
                : string.Concat(_settings.Password.Where(character => !char.IsWhiteSpace(character)));
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(_settings.Username)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(_settings.Username, password)
            };
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Falha ao enviar e-mail de recuperação de senha.");
        }
    }
}
