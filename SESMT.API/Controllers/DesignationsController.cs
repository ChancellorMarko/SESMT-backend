using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Services;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("designations")]
public sealed class DesignationsController(OperationalDesignationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOperationalDesignationRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, actor, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListAsync(actor, ct));
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, RevokeOperationalDesignationRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.RevokeAsync(id, request, actor, ct));
    }

    private bool TryActor(out Guid actor) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
}
