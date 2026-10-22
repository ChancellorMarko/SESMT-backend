using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

/// <summary>Controller para Cargos (Positions).</summary>
[ApiController]
[Route("positions")]
[Authorize]
public sealed class PositionsController : ControllerBase
{
    private readonly IPositionService _service;

    public PositionsController(IPositionService service) => _service = service;

    // ─── POST /positions ────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] PositionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /positions?companyId= ──────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (companyId is null || companyId == Guid.Empty)
            return BadRequest(new ErrorResponse(
                StatusCodes.Status400BadRequest,
                "Parâmetro obrigatório ausente",
                "companyId é obrigatório para listar cargos."));

        var result = await _service.ListByCompanyAsync(
            companyId.Value, pageSize, pageNumber, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── GET /positions/{id} ────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Cargo não encontrado",
                result.Error!));
    }

    // ─── PATCH /positions/{id} ──────────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] PositionUpdateRequest updateData,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, updateData, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── DELETE /positions/{id} (soft delete) ───────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Cargo não encontrado",
                result.Error!));
    }

    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, "Recurso não encontrado"),

            var e when e.Contains("já existe", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status409Conflict, "Recurso duplicado"),

            _   => (StatusCodes.Status400BadRequest, "Dados inválidos")
        };

        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}
