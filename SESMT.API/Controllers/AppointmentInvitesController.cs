using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Appointments;

namespace SESMT.API.Controllers;

[ApiController, Authorize, Route("invites")]
public sealed class AppointmentInvitesController(AppointmentService service) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentInvite request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); return StatusCode(201, await service.CreateInviteAsync(request, actor, ct));
    }
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, RevokeAppointmentInvite request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); await service.RevokeInviteAsync(id, request, actor, ct); return NoContent();
    }
    [AllowAnonymous, HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken ct) => Ok(await service.InviteContextAsync(Token(), ct));
    [AllowAnonymous, HttpPost("appointments")]
    public async Task<IActionResult> RequestAppointment(InviteAppointmentRequest request, CancellationToken ct) =>
        StatusCode(201, await service.CreateFromInviteAsync(Token(), request, ct));
    [AllowAnonymous, HttpGet("appointments/{id:guid}")]
    public async Task<IActionResult> ReadRequest(Guid id, CancellationToken ct) => Ok(await service.GetInviteRequestAsync(Token(), id, ct));
    private string Token() => Request.Headers["X-Invite-Token"].Count == 1 ? Request.Headers["X-Invite-Token"].ToString() : "";
    private IActionResult Unauthenticated()
        => Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
    private bool Actor(out Guid actor) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
}
