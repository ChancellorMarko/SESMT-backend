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
}