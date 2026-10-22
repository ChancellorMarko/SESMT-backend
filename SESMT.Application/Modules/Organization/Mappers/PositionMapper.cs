using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Application.Modules.Organization.Mappers;

public static class PositionMapper
{
    public static Position ToEntity(PositionCreateRequest request) =>
        new(
            companyId: request.CompanyId,
            name: request.Name,
            cbo: request.Cbo,
            description: request.Description);

    public static PositionResponse ToResponse(Position position) =>
        new()
        {
            Id = position.Id,
            CompanyId = position.CompanyId,
            Name = position.Name,
            Cbo = position.CBO,
            Description = position.Description,
            Active = position.Active && position.DeletedAt is null
        };

    public static PositionSummaryResponse ToSummary(Position position) =>
        new()
        {
            Id = position.Id,
            CompanyId = position.CompanyId,
            Name = position.Name,
            Cbo = position.CBO,
            Active = position.Active && position.DeletedAt is null
        };
}
