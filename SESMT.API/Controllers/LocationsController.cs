using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("locations")]
[Authorize]
public sealed class LocationsController : ApiControllerBase
{
    private readonly IUnitService _unitService;
    public LocationsController(IUnitService unitService) => _unitService = unitService;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] UnitCreateRequest request, CancellationToken ct)
    {
        var result = await _unitService.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] string? code,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
        => FromResult(await _unitService.ListAsync(companyId, code, pageSize, pageNumber, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _unitService.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UnitUpdateRequest request, CancellationToken ct)
        => FromResult(await _unitService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => NoContentFromResult(await _unitService.DeactivateAsync(id, ct));
}
