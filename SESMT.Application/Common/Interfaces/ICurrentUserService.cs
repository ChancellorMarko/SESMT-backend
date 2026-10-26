namespace SESMT.Application.Common.Interfaces;

/// <summary>
/// Captura o contexto do usuário autenticado para auditoria.
/// Implementado na API (usa HttpContext). Em background jobs, retorna null.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? TraceId { get; }
    bool IsAuthenticated { get; }
}
