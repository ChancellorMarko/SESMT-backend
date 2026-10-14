namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Define os limites de tentativas de autenticação.
/// </summary>
public sealed record LoginPolicy(
    int MaxAttempts = 5,
    int BlockDurationMinutes = 15);