using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; rules and protected data handling live in <see cref="OccupationalHealthService"/>.</summary>
[ApiController, Authorize, Route("occupational-health")]
public sealed class OccupationalHealthController(OccupationalHealthService aso) : ApiControllerBase
{
    [HttpGet("context")]
    public Task<IActionResult> Context(Guid unitId, CancellationToken ct) => Respond(() => aso.ContextAsync(unitId, ct));
    [HttpGet]
    public Task<IActionResult> List(Guid unitId, CancellationToken ct, string? q = null, string view = "LATEST", string? due = null, int page = 1, int pageSize = 20)
        => Respond(() => aso.ListAsync(unitId, ct, q, view, due, page, pageSize));
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, Guid unitId, CancellationToken ct) => Respond(() => aso.GetAsync(id, unitId, ct));
    [HttpGet("{id:guid}/history")]
    public Task<IActionResult> History(Guid id, Guid unitId, CancellationToken ct) => Respond(() => aso.HistoryAsync(id, unitId, ct));
    [HttpPost]
    public Task<IActionResult> Create(AsoRequest request, CancellationToken ct) => Respond(() => aso.SaveAsync(null, request, ct), 201);
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Edit(Guid id, AsoRequest request, CancellationToken ct) => Respond(() => aso.SaveAsync(id, request, ct));
    [HttpPost("{id:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid id, RevokeAsoRequest request, CancellationToken ct) => Respond(() => aso.RevokeAsync(id, request, ct));
}
