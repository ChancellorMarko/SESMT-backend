using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    public DocumentsController(IDocumentService service) => _service = service;

    // ─── CRUD ───────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentCreateRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? unitId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? documentTypeId,
        [FromQuery] string? status,
        [FromQuery] string? secrecyLevel,
        [FromQuery] string? search,
        [FromQuery] DateOnly? validUntilBefore,
        [FromQuery] int pageSize = 20,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(
            companyId, unitId, departmentId, documentTypeId,
            status, secrecyLevel, search, validUntilBefore,
            pageSize, pageNumber, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new ErrorResponse(404, "Documento não encontrado", result.Error!));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DocumentUpdateRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _service.DeactivateAsync(id, ct);
        return result.IsSuccess ? NoContent() : MapFailure(result.Error!);
    }

    // ─── Fluxo documental (RF-014) ──────────────────────────────────────────
    [HttpPost("{id:guid}/submit-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, CancellationToken ct)
    {
        var result = await _service.SubmitForReviewAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _service.ApproveAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _service.PublishAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── Versões (RF-012, RF-013) ───────────────────────────────────────────
    [HttpPost("{id:guid}/versions")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> AddVersion(
        Guid id,
        [FromForm] Guid authorId,
        [FromForm] string? justification,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ErrorResponse(400, "Arquivo obrigatório", "Envie um arquivo válido."));

        await using var stream = file.OpenReadStream();
        var request = new DocumentVersionCreateRequest { AuthorId = authorId, Justification = justification };

        var result = await _service.AddVersionAsync(
            id, request, stream, file.FileName, file.ContentType, ct);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken ct)
    {
        var result = await _service.ListVersionsAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/approve")]
    public async Task<IActionResult> ApproveVersion(
        Guid id, Guid versionId, [FromBody] DocumentVersionApprovalRequest request, CancellationToken ct)
    {
        var result = await _service.ApproveVersionAsync(id, versionId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/reject")]
    public async Task<IActionResult> RejectVersion(
        Guid id, Guid versionId, [FromBody] DocumentVersionRejectionRequest request, CancellationToken ct)
    {
        var result = await _service.RejectVersionAsync(id, versionId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── Download (RF-061 — gera auditoria) ────────────────────────────────
    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid versionId, CancellationToken ct)
    {
        var result = await _service.DownloadVersionAsync(id, versionId, ct);

        if (!result.IsSuccess)
            return NotFound(new ErrorResponse(404, "Arquivo não encontrado", result.Error!));

        var file = result.Value!;
        return File(file.Content, file.MimeType, file.FileName);
    }

    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase) => (404, "Recurso não encontrado"),
            var e when e.Contains("já existe", StringComparison.OrdinalIgnoreCase) => (409, "Recurso duplicado"),
            var e when e.Contains("não pode", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("Só é possível", StringComparison.OrdinalIgnoreCase) => (422, "Regra de negócio não atendida"),
            _ => (400, "Dados inválidos")
        };
        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}
