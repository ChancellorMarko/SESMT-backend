using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Dashboard;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("dashboard")]
<<<<<<< HEAD
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.GetSummaryAsync(actor, filter, ct));
=======
public sealed class DashboardController : ApiControllerBase
{
    private readonly DashboardService _service;
    public DashboardController(DashboardService service) => _service = service;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.GetSummaryAsync(actor, filter, ct));
>>>>>>> main
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
<<<<<<< HEAD
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListAppointmentsAsync(actor, filter, ct));
=======
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.ListAppointmentsAsync(actor, filter, ct));
>>>>>>> main
    }

    [HttpGet("document-pendencies")]
    public async Task<IActionResult> DocumentPendencies([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
<<<<<<< HEAD
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListDocumentPendenciesAsync(actor, filter, ct));
=======
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.ListDocumentPendenciesAsync(actor, filter, ct));
>>>>>>> main
    }

    [HttpGet("review-pendencies")]
    public async Task<IActionResult> ReviewPendencies([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
<<<<<<< HEAD
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListReviewPendenciesAsync(actor, filter, ct));
=======
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.ListReviewPendenciesAsync(actor, filter, ct));
>>>>>>> main
    }

    [HttpGet("activity-evolution")]
    public async Task<IActionResult> ActivityEvolution(
        [FromQuery] DashboardFilter filter,
        [FromQuery(Name = "equipeId")] Guid? equipeId,
        CancellationToken ct)
    {
<<<<<<< HEAD
        if (!TryActor(out var actor)) return Unauthorized();
        if (equipeId.HasValue && filter.TeamId.HasValue && equipeId != filter.TeamId)
            return BadRequest(new { error = "Informe apenas um valor coerente para equipeId/teamId." });
        if (equipeId.HasValue) filter = filter with { TeamId = equipeId };
        return Ok(await service.GetActivityEvolutionAsync(actor, filter, ct));
    }

    private bool TryActor(out Guid id) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
=======
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");

        if (equipeId.HasValue && filter.TeamId.HasValue && equipeId != filter.TeamId)
            return Failure(400, "INVALID_PARAM", "Informe apenas um valor coerente para equipeId/teamId.");

        if (equipeId.HasValue) filter = filter with { TeamId = equipeId };
        return Ok(await _service.GetActivityEvolutionAsync(actor, filter, ct));
    }

    private bool TryActor(out Guid id)
        => Guid.TryParse(
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out id);
>>>>>>> main
}
