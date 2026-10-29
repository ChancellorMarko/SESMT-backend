using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Application.Modules.Workforce.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("workers")]
[Authorize]
public sealed class WorkersController : ApiControllerBase
{
    private readonly IWorkerService _workerService;
    private readonly IWorkerHistoryService _historyService;

    public WorkersController(
        IWorkerService workerService,
        IWorkerHistoryService historyService)
    {
        _workerService = workerService;
        _historyService = historyService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] WorkerCreateRequest request, CancellationToken ct)
    {
        var result = await _workerService.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? positionId,
        [FromQuery] string? search,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        if (companyId is null || companyId == Guid.Empty)
            return Failure(400, "MISSING_PARAM", "companyId é obrigatório.");

        return FromResult(await _workerService.ListAsync(
            companyId.Value, unitId, departmentId, positionId,
            search, pageSize, pageNumber, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _workerService.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] WorkerUpdateRequest request, CancellationToken ct)
        => FromResult(await _workerService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => NoContentFromResult(await _workerService.DeactivateAsync(id, ct));

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
        => FromResult(await _historyService.ListByWorkerAsync(id, ct));

    [HttpPost("{id:guid}/history")]
    public async Task<IActionResult> AddHistory(
        Guid id, [FromBody] WorkerHistoryCreateRequest request, CancellationToken ct)
    {
        var result = await _historyService.AddManualAsync(id, request, ct);
        if (!result.IsSuccess)
            return Failure(result.Error!);

        return Created($"/api/v1/workers/{id}/history/{result.Value!.Id}", result.Value);
    }
}
