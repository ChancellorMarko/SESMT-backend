using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitServiceResponse(Guid Id, string Code, string Name)
{
    public static WorkPermitServiceResponse From(WorkPermitServiceCatalogEntry service) => new(service.Id, service.Code, service.Name);
}
