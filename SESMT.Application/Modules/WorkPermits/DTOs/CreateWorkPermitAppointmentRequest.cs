using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record CreateWorkPermitAppointmentRequest(
    Guid CompanyId,
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    Guid? LocationId = null);
