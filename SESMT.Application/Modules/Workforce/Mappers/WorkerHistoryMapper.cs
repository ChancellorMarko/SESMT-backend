using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Application.Modules.Workforce.Mappers;

public static class WorkerHistoryMapper
{
    public static WorkerHistory ToEntity(Guid workerId, WorkerHistoryCreateRequest request) =>
        new(
            workerId: workerId,
            departmentId: request.DepartmentId,
            positionId: request.PositionId,
            startDate: request.StartDate,
            endDate: request.EndDate,
            reasonForChange: request.ReasonForChange);

    public static WorkerHistoryResponse ToResponse(WorkerHistory history) =>
        new()
        {
            Id = history.Id,
            WorkerId = history.WorkerId,
            DepartmentId = history.DepartmentId,
            PositionId = history.PositionId,
            StartDate = history.StartDate,
            EndDate = history.EndDate,
            ReasonForChange = history.ReasonForChange,
            IsActive = history.IsActive
        };
}
