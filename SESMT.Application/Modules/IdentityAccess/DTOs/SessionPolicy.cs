namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Define a duração das sessões renováveis e a janela em que o refresh token
/// imediatamente anterior ainda é aceito uma única vez (resposta perdida pelo navegador).
/// </summary>
public sealed record SessionPolicy(
    int RefreshTokenExpirationDays = 7,
    int RefreshReuseGraceSeconds = 30);
