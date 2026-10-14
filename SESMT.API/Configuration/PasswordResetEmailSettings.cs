namespace SESMT.API.Configuration;

public sealed class PasswordResetEmailSettings
{
    public const string SectionName = "PasswordResetEmail";
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string From { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? Password { get; init; }
}
