using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record CreateWorkPermitRequest(
    Guid AppointmentId,
    [Required, StringLength(80, MinimumLength = 1)] string ServiceCode,
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    IReadOnlyList<Guid>? TemplateVersionIds = null);
