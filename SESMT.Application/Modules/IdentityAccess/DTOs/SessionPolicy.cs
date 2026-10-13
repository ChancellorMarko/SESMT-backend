namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Define a duração das sessões renováveis.
/// </summary>
public sealed record SessionPolicy(
    int RefreshTokenExpirationDays = 7);
