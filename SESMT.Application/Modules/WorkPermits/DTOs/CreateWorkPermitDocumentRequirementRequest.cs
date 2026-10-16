using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record CreateWorkPermitDocumentRequirementRequest(
    Guid CompanyId,
    [Required, StringLength(80, MinimumLength = 1)] string ServiceCode,
    Guid DocumentTypeId,
    [Required, StringLength(300, MinimumLength = 1)] string Description);
