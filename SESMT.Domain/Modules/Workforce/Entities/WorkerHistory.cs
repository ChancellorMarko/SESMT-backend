using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Workforce.Entities;

public class WorkerHistory : AggregateRootBase
{
    public Guid WorkerId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid PositionId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? ReasonForChange { get; private set; }

    protected WorkerHistory() { }

    public WorkerHistory(
        Guid workerId,
        Guid departmentId,
        Guid positionId,
        DateOnly startDate,
        DateOnly? endDate = null,
        string? reasonForChange = null)
    {
        if (workerId == Guid.Empty)
            throw new ArgumentException("ID do trabalhador é obrigatório.");
        if (departmentId == Guid.Empty)
            throw new ArgumentException("ID do setor é obrigatório.");
        if (positionId == Guid.Empty)
            throw new ArgumentException("ID do cargo é obrigatório.");
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("Data fim não pode ser anterior à data início.");

        WorkerId = workerId;
        DepartmentId = departmentId;
        PositionId = positionId;
        StartDate = startDate;
        EndDate = endDate;
        ReasonForChange = reasonForChange?.Trim();
    }

    /// <summary>
    /// Fecha o registro de histórico (data fim preenchida).
    /// Chamado quando o trabalhador muda de função/setor.
    /// </summary>
    public void Close(DateOnly endDate, string? reason = null)
    {
        if (EndDate.HasValue)
            throw new InvalidOperationException("Histórico já está fechado.");

        if (endDate < StartDate)
            throw new ArgumentException("Data fim não pode ser anterior à data início.");

        EndDate = endDate;
        if (!string.IsNullOrWhiteSpace(reason))
            ReasonForChange = reason.Trim();

        MarkAsUpdated();
    }

    public bool IsActive => !EndDate.HasValue;

    public void Deactivate() => MarkAsDeleted();
}
