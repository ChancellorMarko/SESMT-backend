using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("documents")]
[Authorize]
[DocumentExceptions]
public sealed class DocumentsController(IDocumentService service, DocumentFileOptions fileOptions, DocumentQueryOptions queryOptions) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(DocumentCreateRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [HttpGet("query")]
    public async Task<IActionResult> List([FromQuery] DocumentSearchRequest request, [FromQuery] int? pageNumber, CancellationToken ct)
    {
        if (Request.Query.Any(item => item.Value.Count != 1)) throw new DocumentOperationException(400, "Não repita os filtros da consulta.");
        if (!Request.Query.ContainsKey("pageSize")) request.PageSize = Math.Min(20, queryOptions.MaxPageSize);
        if (pageNumber.HasValue) request.Page = pageNumber.Value;
        var result = await service.SearchAsync(request, ct);
        var page = result.Value!;
        Response.Headers["X-Total-Count"] = page.Total.ToString(CultureInfo.InvariantCulture);
        // Keep the legacy list response; the proposed page parameter/query route opts into an envelope.
        return Request.Query.ContainsKey("page") || Request.Path.Value!.EndsWith("/query", StringComparison.Ordinal)
            ? Ok(page) : Ok(page.Items);
    }

    [HttpGet("file-policy")]
    public IActionResult FilePolicy() => Ok(new { fileOptions.MaxSizeBytes, fileOptions.AllowedMimeTypes });

    [HttpGet("query-policy")]
    public IActionResult QueryPolicy() => Ok(new { queryOptions.MaxPageSize });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => Ok((await service.GetByIdAsync(id, ct)).Value);

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DocumentUpdateRequest request, CancellationToken ct) => Ok((await service.UpdateAsync(id, request, ct)).Value);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await service.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, CancellationToken ct) => Ok((await service.SubmitForReviewAsync(id, ct)).Value);

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Ok((await service.ApproveAsync(id, ct)).Value);

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct) => Ok((await service.PublishAsync(id, ct)).Value);

    [HttpPost("{id:guid}/versions")]
    [HttpPost("{id:guid}/files")]
    [RequestSizeLimit(51 * 1024 * 1024)]
    public async Task<IActionResult> AddVersion(Guid id, [FromForm] int expectedVersion, [FromForm] Guid? authorId,
        [FromForm] string? justification, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new ErrorResponse(400, "Arquivo obrigatório", "Envie um arquivo válido."));
        if (file.Length > fileOptions.MaxSizeBytes) throw new DocumentOperationException(413, "O arquivo excede o limite configurado.");
        await using var content = file.OpenReadStream();
        var request = new DocumentVersionCreateRequest { ExpectedVersion = expectedVersion, Justification = justification };
        var result = await service.AddVersionAsync(id, request, content, file.FileName, file.ContentType, ct);
        return StatusCode(201, result.Value);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken ct) => Ok((await service.ListVersionsAsync(id, ct)).Value);

    [HttpPost("{id:guid}/versions/{versionId:guid}/review")]
    public async Task<IActionResult> Review(Guid id, Guid versionId, DocumentReviewRequest request, CancellationToken ct) =>
        Ok((await service.ReviewAsync(id, versionId, request, ct)).Value);

    [HttpPost("{id:guid}/versions/{versionNumber:int}/review")]
    public async Task<IActionResult> ReviewNumber(Guid id, int versionNumber, DocumentReviewRequest request, CancellationToken ct)
    {
        if (request.Version != 0 && request.Version != versionNumber) throw new DocumentOperationException(400, "A versão informada não corresponde ao endereço.");
        var version = (await service.ListVersionsAsync(id, ct)).Value!.SingleOrDefault(v => v.VersionNumber == versionNumber);
        if (version is null) throw new DocumentOperationException(404, "Versão não encontrada.");
        return Ok((await service.ReviewAsync(id, version.Id, request with { Version = versionNumber }, ct)).Value);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/approve")]
    public async Task<IActionResult> ApproveVersion(Guid id, Guid versionId, DocumentVersionApprovalRequest request, CancellationToken ct) =>
        Ok((await service.ApproveVersionAsync(id, versionId, request, ct)).Value);

    [HttpPost("{id:guid}/versions/{versionId:guid}/reject")]
    public async Task<IActionResult> RejectVersion(Guid id, Guid versionId, DocumentVersionRejectionRequest request, CancellationToken ct) =>
        Ok((await service.RejectVersionAsync(id, versionId, request, ct)).Value);

    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid versionId, CancellationToken ct)
    {
        var file = (await service.DownloadVersionAsync(id, versionId, ct)).Value!;
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.MimeType, file.FileName);
    }

    [HttpGet("{id:guid}/files/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid id, Guid fileId, CancellationToken ct)
    {
        var file = (await service.DownloadFileAsync(id, fileId, ct)).Value!;
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.MimeType, file.FileName);
    }

    [HttpPost("{id:guid}/access-grants")]
    public async Task<IActionResult> Grant(Guid id, DocumentGrantRequest request, CancellationToken ct) =>
        StatusCode(201, (await service.GrantAsync(id, request, ct)).Value);

    [HttpPost("{id:guid}/access-grants/{grantId:guid}/revoke")]
    public async Task<IActionResult> RevokeGrant(Guid id, Guid grantId, [FromQuery] int expectedVersion, CancellationToken ct)
    {
        await service.RevokeGrantAsync(id, grantId, expectedVersion, ct);
        return NoContent();
    }
}
