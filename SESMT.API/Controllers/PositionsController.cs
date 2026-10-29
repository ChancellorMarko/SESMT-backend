using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("positions")]
[Authorize]
public sealed class PositionsController : ApiControllerBase
{
    private readonly IPositionService _service;
    public PositionsController(IPositionService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PositionCreateRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        if (companyId is null || companyId == Guid.Empty)
            return Failure(400, "MISSING_PARAM", "companyId é obrigatório.");

        return FromResult(await _service.ListByCompanyAsync(
            companyId.Value, pageSize, pageNumber, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] PositionUpdateRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => NoContentFromResult(await _service.DeactivateAsync(id, ct));
}
