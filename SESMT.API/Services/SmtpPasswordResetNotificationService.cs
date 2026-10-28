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
            _logger.LogError(
                "PasswordResetEmail:ResetUrl não configurada. " +
                "O e-mail será enviado sem link clicável.");
        }

        var resetLink = string.IsNullOrWhiteSpace(_settings.ResetUrl)
            ? token
            : $"{_settings.ResetUrl}?token={Uri.EscapeDataString(token)}";

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
            client.Credentials = new NetworkCredential(
                _settings.Username,
                _settings.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation(
                "E-mail de redefinição enviado para {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao enviar e-mail de redefinição para {Email}", email);
        }
    }
}
