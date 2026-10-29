using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Notifications;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("notifications")]
<<<<<<< HEAD
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Unauthorized();
        return Ok(new { items = await service.ListAsync(actor, ct) });
=======
public sealed class NotificationsController : ApiControllerBase
{
    private readonly NotificationService _service;
    public NotificationsController(NotificationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(new { items = await _service.ListAsync(actor, ct) });
>>>>>>> main
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
<<<<<<< HEAD
        if (!TryActor(out var actor)) return Unauthorized();
        return await service.MarkAsReadAsync(actor, id, ct) ? NoContent() : NotFound();
    }

    private bool TryActor(out Guid id) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
=======
        if (!TryActor(out var actor))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");

        return await _service.MarkAsReadAsync(actor, id, ct)
            ? NoContent()
            : Failure(404, "NOTIFICATION_NOT_FOUND", "Notificação não encontrada.");
    }

    private bool TryActor(out Guid id)
        => Guid.TryParse(
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out id);
>>>>>>> main
}
