using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record AddWorkPermitParticipantRequest(Guid UserId, [Required, StringLength(80, MinimumLength = 1)] string Responsibility);
