using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Appointments;

namespace SESMT.API.Controllers;

[ApiController, Authorize, Route("appointments")]
public sealed class AppointmentsController(AppointmentService service) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(RequestAppointment request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated();
        var result = await service.CreateAsync(request, actor, ct);
        return CreatedAtAction(nameof(Get), new {id = result.Id}, result);
    }
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, Guid? companyId = null, DateTimeOffset? from = null, DateTimeOffset? to = null,
        string? status = null, string? q = null, int page = 1, int pageSize = 20)
    {
        if (!Actor(out var actor)) return Unauthenticated();
        return Ok(await service.ListAsync(actor, companyId, from, to, status, q, page, pageSize, ct));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); return Ok(await service.GetAsync(id, actor, ct));
    }
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, ConfirmAppointment request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); return Ok(await service.ConfirmAsync(id, request, actor, ct));
    }
    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleAppointment request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); return Ok(await service.RescheduleAsync(id, request, actor, ct));
    }
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelAppointment request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); return Ok(await service.CancelAsync(id, request, actor, ct));
    }
    [HttpPut("{id:guid}/context")]
    public async Task<IActionResult> Configure(Guid id, ConfigureAppointment request, CancellationToken ct)
    {
        if (!Actor(out var actor)) return Unauthenticated(); return Ok(await service.ConfigureAsync(id, request, actor, ct));
    }
    private IActionResult Unauthenticated()
        => Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
    private bool Actor(out Guid actor) => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out actor) && actor != Guid.Empty;
}
