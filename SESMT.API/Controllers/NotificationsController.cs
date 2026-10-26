using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Notifications;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("notifications")]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(new { items = await service.ListAsync(actor, ct) });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return await service.MarkAsReadAsync(actor, id, ct) ? NoContent() : NotFound();
    }

    private bool TryActor(out Guid id) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}
