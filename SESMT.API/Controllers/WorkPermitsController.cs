using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.WorkPermits.DTOs;
using SESMT.Application.Modules.WorkPermits.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Authorize]
[Route("work-permits")]
public sealed class WorkPermitsController : ApiControllerBase
{
    private readonly IWorkPermitService _service;
    public WorkPermitsController(IWorkPermitService service) => _service = service;

    // ⭐ Rotas SEM barra inicial — todas ficam sob /api/v1/work-permits

    [HttpPost("appointments/{id:guid}/pt")]
    public async Task<IActionResult> OpenAppointment(
        Guid id, OpenWorkPermitRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");

        var permit = await _service.OpenAppointmentAsync(id, request, actorId, ct);
        return StatusCode(StatusCodes.Status201Created, permit);
    }

    [HttpGet("pts/{id:guid}")]
    public async Task<IActionResult> GetChecklist(Guid id, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.GetChecklistAsync(id, actorId, ct));
    }

    [HttpPatch("pts/{id:guid}/checklist")]
    public async Task<IActionResult> PatchChecklist(
        Guid id, PatchWorkPermitChecklistRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.PatchChecklistAsync(id, request, actorId, ct));
    }

    [HttpGet("services")]
    public IActionResult GetServices() => Ok(_service.GetServices());

    [HttpGet("pt-templates")]
    public async Task<IActionResult> ListTemplates(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? templateId,
        CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.ListTemplatesAsync(actorId, companyId, templateId, ct));
    }

    [HttpPost("pt-templates")]
    public async Task<IActionResult> CreateTemplate(
        CreateWorkPermitTemplateRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        var response = await _service.CreateTemplateAsync(request, actorId, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("pt-templates/{id:guid}/versions")]
    public async Task<IActionResult> CreateTemplateVersion(
        Guid id, CreateWorkPermitTemplateVersionRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        var response = await _service.CreateTemplateVersionAsync(id, request, actorId, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkPermitRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");

        var response = await _service.CreateAsync(request, actorId, ct);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPost("appointments")]
    public async Task<IActionResult> CreateAppointment(
        CreateWorkPermitAppointmentRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.CreateAppointmentAsync(request, actorId, ct));
    }

    [HttpPost("requirements")]
    public async Task<IActionResult> ConfigureRequirement(
        CreateWorkPermitDocumentRequirementRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        await _service.ConfigureRequirementAsync(request, actorId, ct);
        return NoContent();
    }

    [HttpGet("requirements")]
    public async Task<IActionResult> GetRequirements(
        [FromQuery] Guid companyId,
        [FromQuery] string serviceCode,
        CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.GetRequirementsAsync(companyId, serviceCode, actorId, ct));
    }

    [HttpPatch("{id:guid}/checklist")]
    public async Task<IActionResult> SaveChecklist(
        Guid id, SaveWorkPermitChecklistRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.SaveChecklistAsync(id, request, actorId, ct));
    }

    [HttpPost("{id:guid}/documents")]
    public async Task<IActionResult> LinkDocument(
        Guid id, LinkWorkPermitDocumentRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        await _service.LinkDocumentAsync(id, request, actorId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/participants")]
    public async Task<IActionResult> AddParticipant(
        Guid id, AddWorkPermitParticipantRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.AddParticipantAsync(id, request, actorId, ct));
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.AcknowledgeAsync(id, actorId, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.ListAsync(actorId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.GetAsync(id, actorId, ct));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await _service.GetHistoryAsync(id, actorId, ct));
    }

    [HttpPost("{id:guid}/release")]
    public Task<IActionResult> Release(Guid id, CancellationToken ct)
        => ExecuteAsync(actorId => _service.ReleaseAsync(id, actorId, ct));

    [HttpPost("{id:guid}/start")]
    public Task<IActionResult> Start(Guid id, CancellationToken ct)
        => ExecuteAsync(actorId => _service.StartAsync(id, actorId, ct));

    [HttpPost("{id:guid}/suspend")]
    public Task<IActionResult> Suspend(
        Guid id, WorkPermitReasonRequest request, CancellationToken ct)
        => ExecuteAsync(actorId => _service.SuspendAsync(id, actorId, request.Reason, ct));

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken ct)
        => ExecuteAsync(actorId => _service.ResumeAsync(id, actorId, ct));

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => ExecuteAsync(actorId => _service.CompleteAsync(id, actorId, ct));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(
        Guid id, WorkPermitReasonRequest request, CancellationToken ct)
        => ExecuteAsync(actorId => _service.CancelAsync(id, actorId, request.Reason, ct));

    private async Task<IActionResult> ExecuteAsync(Func<Guid, Task<WorkPermitResponse>> command)
    {
        if (!TryGetActorId(out var actorId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");
        return Ok(await command(actorId));
    }

    private bool TryGetActorId(out Guid actorId)
    {
        var userId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out actorId);
    }
}
