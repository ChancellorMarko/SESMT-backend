using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

/// <summary>Controller para Departamentos/Setores (Departments).</summary>
[ApiController]
[Route("departments")]
[Authorize]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _service;

    public DepartmentsController(IDepartmentService service) => _service = service;

    // ─── POST /departments ─────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] DepartmentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /departments?unitId= ───────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? unitId,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (unitId is null || unitId == Guid.Empty)
            return BadRequest(new ErrorResponse(
                StatusCodes.Status400BadRequest,
                "Parâmetro obrigatório ausente",
                "unitId é obrigatório para listar departamentos."));

        var result = await _service.ListByUnitAsync(
            unitId.Value, pageSize, pageNumber, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── GET /departments/{id} ─────────────────────────────────────────────
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
                "Departamento não encontrado",
                result.Error!));
    }

    // ─── PATCH /departments/{id} ───────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] DepartmentUpdateRequest updateData,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, updateData, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── DELETE /departments/{id} (soft delete) ────────────────────────────
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
                "Departamento não encontrado",
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
