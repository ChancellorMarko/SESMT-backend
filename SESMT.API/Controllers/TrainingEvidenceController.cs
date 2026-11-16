using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; rules live in <see cref="TrainingEvidenceService"/>.</summary>
[ApiController, Authorize, Route("trainings/{trainingId:guid}/evidence")]
public sealed class TrainingEvidenceController(TrainingEvidenceService evidence) : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(Guid trainingId, Guid unitId, CancellationToken ct) => Respond(() => evidence.GetAsync(trainingId, unitId, ct));
    [HttpGet("options")]
    public Task<IActionResult> Options(Guid trainingId, Guid unitId, CancellationToken ct, string? q = null, int page = 1) => Respond(() => evidence.OptionsAsync(trainingId, unitId, ct, q, page));
    [HttpPost]
    public Task<IActionResult> Bind(Guid trainingId, BindTrainingEvidence request, CancellationToken ct) => Respond(() => evidence.BindAsync(trainingId, request, ct));
    [HttpPost("unlink")]
    public Task<IActionResult> Unlink(Guid trainingId, UnlinkTrainingEvidence request, CancellationToken ct) => Respond(() => evidence.UnlinkAsync(trainingId, request, ct));
}
