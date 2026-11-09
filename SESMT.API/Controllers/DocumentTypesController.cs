using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;
using SESMT.Application.Modules.Documents.Services;

namespace SESMT.API.Controllers;

[ApiController]
[Route("document-types")]
[Authorize]
public sealed class DocumentTypesController : ApiControllerBase
{
    private readonly IDocumentTypeService _service;
    public DocumentTypesController(IDocumentTypeService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] DocumentTypeCreateRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return InvalidActor();
        var result = await _service.CreateAsync(actorId, request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => FromResult(await _service.ListPagedAsync(q, page, pageSize, ct));

    [HttpGet("management-context")]
    public async Task<IActionResult> ManagementContext(CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return InvalidActor();
        return FromResult(await _service.GetManagementContextAsync(actorId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (!result.IsSuccess) return FromResult(result);
        var item = result.Value!;
        return Ok(new DocumentTypeDetailResponse(item, item.Version));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
        => FromResult(await _service.GetHistoryAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] DocumentTypeUpdateRequest request, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return InvalidActor();
        if (request.ExpectedVersion < 1)
            return Failure(409, "VERSION_REQUIRED",
                "Informe expectedVersion para editar um tipo documental.");

        var result = await _service.UpdateAsync(actorId, id, request, ct);
        return result.Error switch
        {
            DocumentTypeService.VersionConflictError => Failure(409, "VERSION_CONFLICT", result.Error),
            DocumentTypeService.ImmutablePolicyError => Failure(409, "IMMUTABLE_DOCUMENT_POLICY", result.Error),
            _ => FromResult(result)
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!TryGetActor(out var actorId)) return InvalidActor();
        return NoContentFromResult(await _service.DeactivateAsync(actorId, id, ct));
    }

    private IActionResult InvalidActor() =>
        Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário.");

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out actorId);
}
