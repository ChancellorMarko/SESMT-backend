using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;

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
        var result = await _service.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category, [FromQuery] bool? onlyActive = true,
        CancellationToken ct = default)
        => FromResult(await _service.ListAsync(category, onlyActive, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] DocumentTypeUpdateRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => NoContentFromResult(await _service.DeactivateAsync(id, ct));
}
