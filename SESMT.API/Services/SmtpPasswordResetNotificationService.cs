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
            var resetLink = BuildResetLink(_settings.ResetUrl, token);
            var body = resetLink is null
                ? $"Use este token para redefinir sua senha. Ele expira em 30 minutos:\n\n{token}"
                : $"Acesse o link abaixo para redefinir sua senha. Ele expira em 30 minutos:\n\n{resetLink}";
            using var message = new MailMessage(_settings.From, email)
            {
                Subject = "Redefinição de senha SESMT",
                Body = body,
                IsBodyHtml = false
            };
            var password = string.IsNullOrWhiteSpace(_settings.Password) ? null : _settings.Password.Trim();
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
            // Keep the generic forgot-password response and never put the reset token,
            // recipient address, SMTP credentials, or exception payload in logs.
            _logger.LogError("Falha ao enviar e-mail de recuperação de senha. Tipo: {FailureType}.",
                exception.GetType().Name);
        }
    }

    private static string? BuildResetLink(string resetUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(resetUrl)) return null;
        var builder = new UriBuilder(resetUrl);
        var tokenParameter = $"token={Uri.EscapeDataString(token)}";
        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? tokenParameter
            : $"{builder.Query.TrimStart('?')}&{tokenParameter}";
        return builder.Uri.AbsoluteUri;
    }
}
