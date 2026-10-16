namespace SESMT.Application.Modules.WorkPermits.Services;

public sealed class WorkPermitNotReadyException : Exception
{
    public WorkPermitNotReadyException()
        : base("A permissão possui pendências que impedem esta ação.")
    {
    }
}
