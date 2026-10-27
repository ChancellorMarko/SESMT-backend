using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
public sealed class TeamsController : ApiControllerBase
{
    private readonly ITeamService _teamService;
    public TeamsController(ITeamService teamService) => _teamService = teamService;

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => NoContentFromResult(await _teamService.DeactivateAsync(id, ct));
}
