using System.ComponentModel.DataAnnotations;

namespace SESMT.Application.Modules.WorkPermits.DTOs;

public sealed record LinkWorkPermitDocumentRequest(Guid DocumentId, [Range(1, int.MaxValue)] int ExpectedVersion = 0);
