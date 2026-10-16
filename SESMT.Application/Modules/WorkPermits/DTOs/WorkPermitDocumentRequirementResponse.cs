using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitDocumentRequirementResponse(Guid Id, Guid CompanyId, string ServiceCode, Guid DocumentTypeId, string Description)
{
    public static WorkPermitDocumentRequirementResponse From(WorkPermitDocumentRequirement requirement) =>
        new(requirement.Id, requirement.CompanyId, requirement.ServiceCode, requirement.DocumentTypeId, requirement.Description);
}
