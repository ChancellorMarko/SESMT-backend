using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("document-types")]
[Authorize]
public sealed class DocumentTypesController : ControllerBase
{
    private readonly IDocumentTypeService _service;
    public DocumentTypesController(IDocumentTypeService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] DocumentTypeCreateRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category, [FromQuery] bool? onlyActive = true,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(category, onlyActive, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new ErrorResponse(404, "Tipo não encontrado", result.Error!));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DocumentTypeUpdateRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _service.DeactivateAsync(id, ct);
        return result.IsSuccess ? NoContent() : MapFailure(result.Error!);
    }

    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase) => (404, "Recurso não encontrado"),
            var e when e.Contains("já existe", StringComparison.OrdinalIgnoreCase) => (409, "Recurso duplicado"),
            _ => (400, "Dados inválidos")
        };
        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}
