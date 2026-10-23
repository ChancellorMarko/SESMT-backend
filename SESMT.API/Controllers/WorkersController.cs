using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Workforce.DTOs;
using SESMT.Application.Modules.Workforce.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("workers")]
[Authorize]
public sealed class WorkersController : ControllerBase
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

    // ─── POST /workers ──────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] WorkerCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workerService.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /workers?companyId=... ─────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? positionId,
        [FromQuery] string? search,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (companyId is null || companyId == Guid.Empty)
            return BadRequest(new ErrorResponse(
                StatusCodes.Status400BadRequest,
                "Parâmetro obrigatório ausente",
                "companyId é obrigatório para listar trabalhadores."));

        var result = await _workerService.ListAsync(
            companyId.Value, unitId, departmentId, positionId,
            search, pageSize, pageNumber, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── GET /workers/{id} ──────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workerService.GetByIdAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Trabalhador não encontrado",
                result.Error!));
    }

    // ─── PATCH /workers/{id} ────────────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] WorkerUpdateRequest updateData,
        CancellationToken cancellationToken)
    {
        var result = await _workerService.UpdateAsync(id, updateData, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── DELETE /workers/{id} (soft delete) ─────────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workerService.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Trabalhador não encontrado",
                result.Error!));
    }

    // ─── GET /workers/{id}/history ──────────────────────────────────────────
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _historyService.ListByWorkerAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Trabalhador não encontrado",
                result.Error!));
    }

    // ─── POST /workers/{id}/history ─────────────────────────────────────────
    [HttpPost("{id:guid}/history")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddHistory(
        Guid id,
        [FromBody] WorkerHistoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _historyService.AddManualAsync(id, request, cancellationToken);

        return result.IsSuccess
            ? Created($"/workers/{id}/history/{result.Value!.Id}", result.Value)
            : MapFailure(result.Error!);
    }

    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, "Recurso não encontrado"),

            var e when e.Contains("já existe", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status409Conflict, "Recurso duplicado"),

            var e when e.Contains("CPF inválido", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("matrícula", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio não atendida"),

            _   => (StatusCodes.Status400BadRequest, "Dados inválidos")
        };

        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}
