using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Workforce.Entities;

public class WorkerHistory : AggregateRootBase
{
    public Guid WorkerId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid PositionId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? ReasonForChange { get; private set; }

    protected WorkerHistory() { }

    public WorkerHistory(Guid workerId, Guid departmentId, Guid positionId, DateTime startDate, DateTime? endDate = null, string? reasonForChange = null)
    {
        WorkerId = workerId;
        DepartmentId = departmentId;
        PositionId = positionId;
        StartDate = startDate;
        EndDate = endDate;
        ReasonForChange = reasonForChange;
    }

    public void UpdateHistory(Guid departmentId, Guid positionId, DateTime startDate, DateTime? endDate = null, string? reasonForChange = null)
    {
        DepartmentId = departmentId;
        PositionId = positionId;
        StartDate = startDate;
        EndDate = endDate;
        ReasonForChange = reasonForChange;

        MarkAsUpdated();
    }
}