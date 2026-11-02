namespace SESMT.Application.Exceptions;

public sealed class ResourceForbiddenException : Exception
{
    public ResourceForbiddenException() : base("Você não tem permissão para esta operação.") { }
}
