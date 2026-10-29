using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("documents")]
[Authorize]
public sealed class DocumentsController : ApiControllerBase
{
    private readonly IDocumentService _service;

    public DocumentsController(IDocumentService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] DocumentCreateRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
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
        if (companyId == Guid.Empty)
            return Failure(400, "MISSING_PARAM", "companyId é obrigatório.");

        var result = await _service.ListAsync(
            companyId, unitId, departmentId, documentTypeId,
            status, secrecyLevel, search, validUntilBefore,
            pageSize, pageNumber, ct);

        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] DocumentUpdateRequest request, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => NoContentFromResult(await _service.DeactivateAsync(id, ct));

    [HttpPost("{id:guid}/submit-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, CancellationToken ct)
        => FromResult(await _service.SubmitForReviewAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => FromResult(await _service.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => FromResult(await _service.PublishAsync(id, ct));

    [HttpPost("{id:guid}/versions")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> AddVersion(
        Guid id,
        [FromForm] Guid authorId,
        [FromForm] string? justification,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Failure(400, "FILE_REQUIRED", "Envie um arquivo válido.");

        await using var stream = file.OpenReadStream();
        var request = new DocumentVersionCreateRequest
        {
            AuthorId = authorId,
            Justification = justification,
        };

        var result = await _service.AddVersionAsync(
            id, request, stream, file.FileName, file.ContentType, ct);

        return FromResult(result);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken ct)
        => FromResult(await _service.ListVersionsAsync(id, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/approve")]
    public async Task<IActionResult> ApproveVersion(
        Guid id, Guid versionId,
        [FromBody] DocumentVersionApprovalRequest request, CancellationToken ct)
        => FromResult(await _service.ApproveVersionAsync(id, versionId, request, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/reject")]
    public async Task<IActionResult> RejectVersion(
        Guid id, Guid versionId,
        [FromBody] DocumentVersionRejectionRequest request, CancellationToken ct)
        => FromResult(await _service.RejectVersionAsync(id, versionId, request, ct));

    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid versionId, CancellationToken ct)
    {
        var result = await _service.DownloadVersionAsync(id, versionId, ct);

        if (!result.IsSuccess)
            return Failure(404, "FILE_NOT_FOUND", result.Error!);

        var file = result.Value!;
        return File(file.Content, file.MimeType, file.FileName);
    }
}
