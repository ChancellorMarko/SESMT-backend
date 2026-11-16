using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; scope, rules, revision control and audit live in <see cref="TrainingService"/>.</summary>
[ApiController, Authorize, Route("trainings")]
public sealed class TrainingsController(TrainingService trainings) : ApiControllerBase
{
    [HttpGet("context")]
    public Task<IActionResult> Context(Guid unitId, CancellationToken ct) => Run(() => trainings.ContextAsync(unitId, ct));

    [HttpGet]
    public Task<IActionResult> List(Guid unitId, CancellationToken ct, string? q = null, string? status = null,
        Guid? workerId = null, int page = 1, int pageSize = 20) => Run(() => trainings.ListAsync(unitId, q, status, workerId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, Guid unitId, CancellationToken ct) => Run(() => trainings.GetAsync(id, unitId, ct));

    [HttpGet("{id:guid}/history")]
    public Task<IActionResult> History(Guid id, Guid unitId, CancellationToken ct) => Run(() => trainings.HistoryAsync(id, unitId, ct));

    [HttpPost]
    public Task<IActionResult> Create(TrainingRequest request, CancellationToken ct) => Run(() => trainings.SaveAsync(null, request, ct), 201);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Edit(Guid id, TrainingRequest request, CancellationToken ct) => Run(() => trainings.SaveAsync(id, request, ct));

    [HttpPost("{id:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid id, RevokeTrainingRequest request, CancellationToken ct) => Run(() => trainings.RevokeAsync(id, request, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action, int status = 200)
    {
        Response.Headers.CacheControl = "no-store";
        try { return StatusCode(status, await action()); }
        catch (TrainingRefusal refusal) { return Failure(refusal.Status, refusal.Code, refusal.Message); }
    }
}
