namespace SESMT.Application.Modules.WorkPermits.Services;

public sealed class WorkPermitForbiddenException : Exception
{
    public WorkPermitForbiddenException()
        : base("O usuário não possui a designação necessária para esta empresa.")
    {
    }
}
