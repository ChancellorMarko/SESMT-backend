namespace SESMT.Domain.Exceptions;

/// <summary>
/// Representa uma violação de regra de negócio do domínio.
/// Exceções desse tipo podem ser tratadas pela camada de aplicação
/// sem expor detalhes internos do domínio para a API.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}