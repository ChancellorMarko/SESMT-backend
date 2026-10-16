using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("work-permits")]
public sealed class WorkPermitsController : ControllerBase
{
    private readonly IWorkPermitService _service;

    public WorkPermitsController(IWorkPermitService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(WorkPermitResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateWorkPermitRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        var response = await _service.CreateAsync(request, actorId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPost("appointments")]
    [ProducesResponseType(typeof(WorkPermitAppointmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAppointment(CreateWorkPermitAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.CreateAppointmentAsync(request, actorId, cancellationToken));
    }

    [HttpPost("requirements")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfigureRequirement(CreateWorkPermitDocumentRequirementRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        await _service.ConfigureRequirementAsync(request, actorId, cancellationToken);
        return NoContent();
    }

    [HttpGet("requirements")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkPermitDocumentRequirementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequirements([FromQuery] Guid companyId, [FromQuery] string serviceCode, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.GetRequirementsAsync(companyId, serviceCode, actorId, cancellationToken));
    }

    [HttpPost("{id:guid}/documents")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkDocument(Guid id, LinkWorkPermitDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        await _service.LinkDocumentAsync(id, request, actorId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/participants")]
    [ProducesResponseType(typeof(WorkPermitParticipantResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddParticipant(Guid id, AddWorkPermitParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.AddParticipantAsync(id, request, actorId, cancellationToken));
    }

    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(WorkPermitParticipantResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.AcknowledgeAsync(id, actorId, cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkPermitResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.ListAsync(actorId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkPermitResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.GetAsync(id, actorId, cancellationToken));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await _service.GetHistoryAsync(id, actorId, cancellationToken));
    }

    [HttpPost("{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(actorId => _service.ReleaseAsync(id, actorId, cancellationToken));

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(actorId => _service.StartAsync(id, actorId, cancellationToken));

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, WorkPermitReasonRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(actorId => _service.SuspendAsync(id, actorId, request.Reason, cancellationToken));

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(actorId => _service.ResumeAsync(id, actorId, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(actorId => _service.CompleteAsync(id, actorId, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, WorkPermitReasonRequest request, CancellationToken cancellationToken) =>
        await ExecuteAsync(actorId => _service.CancelAsync(id, actorId, request.Reason, cancellationToken));

    private async Task<IActionResult> ExecuteAsync(Func<Guid, Task<WorkPermitResponse>> command)
    {
        if (!TryGetActorId(out var actorId))
            return Unauthorized();

        return Ok(await command(actorId));
    }

    private bool TryGetActorId(out Guid actorId)
    {
        var userId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out actorId);
    }
}
