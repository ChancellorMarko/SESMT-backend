using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Services;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("designations")]
public sealed class DesignationsController : ApiControllerBase
{
    private readonly OperationalDesignationService _service;
    public DesignationsController(OperationalDesignationService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOperationalDesignationRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(request, actor, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.ListAsync(actor, ct));
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(
        Guid id, RevokeOperationalDesignationRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.RevokeAsync(id, request, actor, ct));
    }

    private bool TryActor(out Guid actor)
        => Guid.TryParse(
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actor);
}
