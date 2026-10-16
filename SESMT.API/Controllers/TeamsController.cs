using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

/// <summary>Controller para Equipes (Teams).</summary>
[ApiController]
[Route("teams")]
[Authorize]
public sealed class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService) => _teamService = teamService;

    // ─── POST /teams ────────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] TeamCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _teamService.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /teams?locationId={guid}&pageSize=&pageNumber= ─────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? locationId,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (locationId is null || locationId == Guid.Empty)
            return BadRequest(new ErrorResponse(
                StatusCodes.Status400BadRequest,
                "Parâmetro obrigatório ausente",
                "locationId é obrigatório para listar equipes."));

        var result = await _teamService.ListByLocationAsync(
            locationId.Value, pageSize, pageNumber, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── GET /teams/{id} ────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _teamService.GetByIdAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Equipe não encontrada",
                result.Error!));
    }

    // ─── PATCH /teams/{id} ──────────────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] TeamPatchRequest patchData,
        CancellationToken cancellationToken)
    {
        var result = await _teamService.UpdateAsync(id, patchData, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── DELETE /teams/{id} (soft delete) ───────────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _teamService.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Equipe não encontrada",
                result.Error!));
    }

    /// <summary>
    /// Mapeia a mensagem de erro do domínio para um status HTTP adequado.
    /// </summary>
    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, "Recurso não encontrado"),

            var e when e.Contains("vínculo", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("prestadora", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("concessão", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio não atendida"),

            _   => (StatusCodes.Status400BadRequest, "Dados inválidos")
        };

        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}