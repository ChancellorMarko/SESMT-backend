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
    [HttpGet]
    public async Task<IActionResult> List(Guid companyId, Guid teamId, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated();
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.ListInvitesAsync(companyId, teamId, actor, ct));
    }
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, RevokeAppointmentInvite request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); await service.RevokeInviteAsync(id, request, actor, ct); return NoContent();
    }
    [AllowAnonymous, HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.InviteContextAsync(Token(), ct));
    }
    [AllowAnonymous, HttpPost("appointments")]
    public async Task<IActionResult> RequestAppointment(InviteAppointmentRequest request, CancellationToken ct,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
    {
        var result = await service.CreateFromInviteAsync(Token(), request, idempotencyKey, ct);
        if (result.Replayed) Response.Headers["Idempotent-Replayed"] = "true";
        return StatusCode(201, result.Appointment);
    }
    [AllowAnonymous, HttpGet("appointments/{id:guid}")]
    public async Task<IActionResult> ReadRequest(Guid id, CancellationToken ct) => Ok(await service.GetInviteRequestAsync(Token(), id, ct));
    private string Token() => Request.Headers["X-Invite-Token"].Count == 1 ? Request.Headers["X-Invite-Token"].ToString() : "";
    private IActionResult Unauthenticated()
        => Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
    private bool Actor(out Guid actor) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
}
