using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record WorkPermitReasonRequest([Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [Range(1, int.MaxValue)] int ExpectedVersion = 0);
