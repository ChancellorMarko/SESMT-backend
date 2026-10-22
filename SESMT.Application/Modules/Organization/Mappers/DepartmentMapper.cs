using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Application.Modules.Organization.Mappers;

public static class DepartmentMapper
{
    public static Department ToEntity(DepartmentCreateRequest request) =>
        new(
            unitId: request.UnitId,
            name: request.Name,
            description: request.Description,
            responsibleId: request.ResponsibleId);

    public static DepartmentResponse ToResponse(Department department) =>
        new()
        {
            Id = department.Id,
            UnitId = department.UnitId,
            Name = department.Name,
            Description = department.Description,
            ResponsibleId = department.ResponsibleId,
            Active = department.Active && department.DeletedAt is null
        };

    public static DepartmentSummaryResponse ToSummary(Department department) =>
        new()
        {
            Id = department.Id,
            UnitId = department.UnitId,
            Name = department.Name,
            Active = department.Active && department.DeletedAt is null
        };
}
