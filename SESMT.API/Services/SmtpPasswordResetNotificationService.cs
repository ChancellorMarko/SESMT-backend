using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SESMT.API.Configuration;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.API.Services;

public sealed class SmtpPasswordResetNotificationService
    : IPasswordResetNotificationService
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

    public async Task SendAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ResetUrl))
        {
            _logger.LogError("PasswordResetEmail:ResetUrl não configurada.");
        }

        // ⭐ Constrói a URL com o token no fragmento (#token=...)
        var uri = new UriBuilder(_settings.ResetUrl);
        uri.Fragment = $"token={Uri.EscapeDataString(token)}";
        var resetLink = uri.Uri.AbsoluteUri;

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.From, "SESMT"),
            Subject = "Redefinição de senha — SESMT",
            IsBodyHtml = false,
            Body = $"""
                Olá,

                Recebemos um pedido para redefinir sua senha no SESMT.

                Clique no link abaixo para criar uma nova senha (válido por 30 minutos):
                {resetLink}

                Se você não solicitou esta alteração, ignore este e-mail.

                — Equipe SESMT
                """,
        };
        message.To.Add(email);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        try
        {
<<<<<<< HEAD
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
=======
>>>>>>> main
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("E-mail de redefinição enviado para {Email}", email);
        }
        catch (Exception ex)
        {
<<<<<<< HEAD
            // Keep the generic forgot-password response and never put the reset token,
            // recipient address, SMTP credentials, or exception payload in logs.
            _logger.LogError("Falha ao enviar e-mail de recuperação de senha. Tipo: {FailureType}.",
                exception.GetType().Name);
=======
            // Silencia a exceção para não revelar se o e-mail existe ou falhou no endpoint público
            _logger.LogError(ex, "Falha ao enviar e-mail de redefinição para {Email}", email);
>>>>>>> main
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
