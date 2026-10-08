using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Domain.Modules.Workforce.Entities;

public class WorkerHistory : AggregateRootBase
{
    public Guid WorkerId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? ReasonForChange { get; private set; }

    protected WorkerHistory() { }

    public WorkerHistory(Guid workerId, Guid departmentId, Guid roleId, DateTime startDate, DateTime? endDate = null, string? reasonForChange = null)
    {
        WorkerId = workerId;
        DepartmentId = departmentId;
        RoleId = roleId;
        StartDate = startDate;
        EndDate = endDate;
        ReasonForChange = reasonForChange;
    }

    public void UpdateHistory(Guid departmentId, Guid roleId, DateTime startDate, DateTime? endDate = null, string? reasonForChange = null)
    {
        DepartmentId = departmentId;
        RoleId = roleId;
        StartDate = startDate;
        EndDate = endDate;
        ReasonForChange = reasonForChange;
    }
}