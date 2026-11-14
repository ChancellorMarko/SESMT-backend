using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
public sealed class TeamsController : ApiControllerBase
{
    private readonly ITeamService _teamService;
    private readonly TeamManagementContextService _contexts;
    private readonly TeamDeactivationService _deactivation;
    public TeamsController(ITeamService teamService, TeamManagementContextService contexts, TeamDeactivationService deactivation)
    { _teamService = teamService; _contexts = contexts; _deactivation = deactivation; }

    [HttpGet("management-context")]
    public async Task<IActionResult> Context([FromQuery] Guid locationId, [FromQuery] string action, CancellationToken ct)
        => Ok(await _contexts.ReadAsync(locationId, action, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TeamCreateRequest request, CancellationToken ct)
    {
        var result = await _teamService.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? locationId,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        if (locationId is null || locationId == Guid.Empty)
            return Failure(400, "MISSING_PARAM", "locationId é obrigatório.");

        return FromResult(await _teamService.ListByLocationAsync(
            locationId.Value, pageSize, pageNumber, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _teamService.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] TeamPatchRequest request, CancellationToken ct)
        => FromResult(await _teamService.UpdateAsync(id, request, ct));

    [HttpGet("{id:guid}/deactivation-impact")]
    public async Task<IActionResult> DeactivationImpact(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _deactivation.ReadImpactAsync(id, ct));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateTeamRequest request, CancellationToken ct)
    {
        try { return Ok(await _deactivation.DeactivateAsync(id, request, ct)); }
        catch (TeamHasPendingAppointmentsException blocked)
        {
            return StatusCode(409, new { code = "TEAM_HAS_PENDING_APPOINTMENTS", message = blocked.Message,
                requestId = HttpContext.TraceIdentifier, impact = blocked.Impact });
        }
    }

    /// <summary>Unconfirmed deletion is no longer accepted: it skipped revision, reason, audit and impact checks.</summary>
    [HttpDelete("{id:guid}")]
    public IActionResult DeactivateWithoutConfirmation(Guid id)
        => Failure(400, "TEAM_DEACTIVATION_CONFIRMATION_REQUIRED",
            "Use POST /teams/{id}/deactivate com a revisão esperada e o motivo.");
}
