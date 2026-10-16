using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitReasonRequest([Required, StringLength(1000, MinimumLength = 1)] string Reason);
