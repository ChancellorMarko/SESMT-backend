using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.Workforce.DTOs;

public class WorkerHistoryCreateRequest
{
    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [StringLength(200)]
    public string? ReasonForChange { get; set; }
}

public class WorkerHistoryResponse
{
    public Guid Id { get; set; }
    public Guid WorkerId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid PositionId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? ReasonForChange { get; set; }
    public bool IsActive { get; set; }
}
