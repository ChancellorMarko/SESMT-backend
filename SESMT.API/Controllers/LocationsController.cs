using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

/// <summary>Controller para Unidades/Localidades (Units).</summary>
[ApiController]
[Route("locations")]
[Authorize]
public sealed class LocationsController : ControllerBase
{
    private readonly IUnitService _unitService;

    public LocationsController(IUnitService unitService) => _unitService = unitService;

    // ─── POST /locations ────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] UnitCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _unitService.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /locations?companyId=&code=&pageSize=&pageNumber= ─────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] string? code,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await _unitService.ListAsync(
            companyId, code, pageSize, pageNumber, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /locations/{id} ────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _unitService.GetByIdAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Unidade não encontrada",
                result.Error!));
    }

    // ─── PATCH /locations/{id} ──────────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UnitUpdateRequest updateData,
        CancellationToken cancellationToken)
    {
        var result = await _unitService.UpdateAsync(id, updateData, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── DELETE /locations/{id} (soft delete) ───────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _unitService.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Unidade não encontrada",
                result.Error!));
    }

    // ─── Helper: mapeia erro do domínio → HTTP status ───────────────────────
    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, "Recurso não encontrado"),

            var e when e.Contains("já existe", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("código", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status409Conflict, "Recurso duplicado"),

            var e when e.Contains("não é permitido", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("empresa", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio não atendida"),

            _   => (StatusCodes.Status400BadRequest, "Dados inválidos")
        };

        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}