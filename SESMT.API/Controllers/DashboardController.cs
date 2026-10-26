using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Dashboard;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("dashboard")]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.GetSummaryAsync(actor, filter, ct));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListAppointmentsAsync(actor, filter, ct));
    }

    [HttpGet("document-pendencies")]
    public async Task<IActionResult> DocumentPendencies([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListDocumentPendenciesAsync(actor, filter, ct));
    }

    [HttpGet("review-pendencies")]
    public async Task<IActionResult> ReviewPendencies([FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(await service.ListReviewPendenciesAsync(actor, filter, ct));
    }

    private bool TryActor(out Guid id) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}
