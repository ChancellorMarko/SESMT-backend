using SESMT.Domain.Common.Entity;
using SESMT.Domain.Exceptions;

namespace SESMT.Domain.Modules.Workforce.Entities;

public sealed class WorkerMovement : EntityBase
{
    public Guid WorkerId { get; private set; }

    public Guid FunctionId { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? EndDate { get; private set; }

    private WorkerMovement()
    {
    }

    public WorkerMovement(
        Guid workerId,
        Guid functionId,
        DateTime startDate)
    {
        WorkerId = workerId;
        FunctionId = functionId;
        StartDate = startDate;
    }

    /// <summary>
    /// Encerra a movimentação atual do trabalhador.
    /// A data de término não pode ser anterior à data de início,
    /// pois isso produziria um histórico inconsistente.
    /// </summary>
    public void End(DateTime endDate)
    {
        if (endDate < StartDate)
        {
            throw new DomainException(
                "A data de término não pode ser anterior à data de início.");
        }

        EndDate = endDate;
    }
}