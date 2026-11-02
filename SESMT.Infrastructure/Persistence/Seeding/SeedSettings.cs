namespace SESMT.Infrastructure.Persistence.Seeding;

/// <summary>
/// Configurações do seed inicial. Lidas de appsettings.
/// </summary>
public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = "admin@sesmt.local";
    public string AdminPassword { get; set; } = "Admin@123";
    public string AdminName { get; set; } = "Administrador do Sistema";

    public bool BootstrapAdminEnabled { get; set; } = true;

    public bool HasSafeProductionBootstrap()
        => !BootstrapAdminEnabled || !string.IsNullOrWhiteSpace(AdminName)
            && System.Net.Mail.MailAddress.TryCreate(AdminEmail, out _)
            && !string.Equals(AdminEmail.Trim(), "admin@sesmt.local", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(AdminPassword) && AdminPassword.Length >= 12
            && !string.Equals(AdminPassword, "Admin@123", StringComparison.Ordinal);
}
