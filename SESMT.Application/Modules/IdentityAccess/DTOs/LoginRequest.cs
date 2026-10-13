namespace SESMT.Application.Modules.IdentityAccess.DTOs;

/// <summary>
/// Representa as credenciais enviadas para autenticação.
/// </summary>
public sealed record LoginRequest(
    string Email,
    string Password);