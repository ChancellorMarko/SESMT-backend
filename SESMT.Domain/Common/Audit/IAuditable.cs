namespace SESMT.Domain.Common.Audit;

/// <summary>
/// Marca uma entidade como passível de auditoria automática.
/// Usado pelo interceptor do EF Core para gerar AuditLog sem precisar
/// de código manual em cada caso de uso.
/// </summary>
public interface IAuditable
{
    Guid Id { get; }
}