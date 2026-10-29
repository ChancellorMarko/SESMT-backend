using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Documents.DTOs;
using SESMT.Application.Modules.Documents.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("documents")]
[Authorize]
[DocumentExceptions]
public sealed class DocumentsController(
    IDocumentService service,
    DocumentFileOptions fileOptions,
    DocumentQueryOptions queryOptions) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] DocumentCreateRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    // GET /documents mantém a resposta legada (lista simples + X-Total-Count).
    // GET /documents/query e o parâmetro "page" optam pelo envelope paginado.
    [HttpGet]
    [HttpGet("query")]
    public async Task<IActionResult> List(
        [FromQuery] DocumentSearchRequest request,
        [FromQuery] int? pageNumber,
        CancellationToken ct)
    {
        var isQueryRoute = Request.Path.Value!.EndsWith("/query", StringComparison.Ordinal);

        // Contrato da main: a listagem legada exige companyId.
        if (!isQueryRoute && request.CompanyId is null)
            return Failure(400, "MISSING_PARAM", "companyId é obrigatório.");

        if (Request.Query.Any(item => item.Value.Count != 1))
            throw new DocumentOperationException(400, "Não repita os filtros da consulta.");

        if (!Request.Query.ContainsKey("pageSize"))
            request.PageSize = Math.Min(20, queryOptions.MaxPageSize);
        if (pageNumber.HasValue)
            request.Page = pageNumber.Value;

        var result = await service.SearchAsync(request, ct);
        if (!result.IsSuccess)
            return FromResult(result);

        var page = result.Value!;
        Response.Headers["X-Total-Count"] = page.Total.ToString(CultureInfo.InvariantCulture);

        return Request.Query.ContainsKey("page") || isQueryRoute
            ? Ok(page)
            : Ok(page.Items);
    }

    [HttpGet("file-policy")]
    public IActionResult FilePolicy()
        => Ok(new { fileOptions.MaxSizeBytes, fileOptions.AllowedMimeTypes });

    [HttpGet("query-policy")]
    public IActionResult QueryPolicy()
        => Ok(new { queryOptions.MaxPageSize });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await service.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] DocumentUpdateRequest request, CancellationToken ct)
        => FromResult(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => NoContentFromResult(await service.DeactivateAsync(id, ct));

    [HttpPost("{id:guid}/submit-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, CancellationToken ct)
        => FromResult(await service.SubmitForReviewAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => FromResult(await service.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => FromResult(await service.PublishAsync(id, ct));

    [HttpPost("{id:guid}/versions")]
    [HttpPost("{id:guid}/files")]
    [RequestSizeLimit(51 * 1024 * 1024)]
    public async Task<IActionResult> AddVersion(
        Guid id,
        [FromForm] int expectedVersion,
        [FromForm] Guid? authorId,
        [FromForm] string? justification,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Failure(400, "FILE_REQUIRED", "Envie um arquivo válido.");

        if (file.Length > fileOptions.MaxSizeBytes)
            throw new DocumentOperationException(413, "O arquivo excede o limite configurado.");

        await using var content = file.OpenReadStream();
        var request = new DocumentVersionCreateRequest
        {
            ExpectedVersion = expectedVersion,
            Justification = justification,
        };

        var result = await service.AddVersionAsync(
            id, request, content, file.FileName, file.ContentType, ct);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FromResult(result);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken ct)
        => FromResult(await service.ListVersionsAsync(id, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid id, Guid versionId,
        [FromBody] DocumentReviewRequest request, CancellationToken ct)
        => FromResult(await service.ReviewAsync(id, versionId, request, ct));

    [HttpPost("{id:guid}/versions/{versionNumber:int}/review")]
    public async Task<IActionResult> ReviewNumber(
        Guid id, int versionNumber,
        [FromBody] DocumentReviewRequest request, CancellationToken ct)
    {
        if (request.Version != 0 && request.Version != versionNumber)
            throw new DocumentOperationException(400, "A versão informada não corresponde ao endereço.");

        var versions = await service.ListVersionsAsync(id, ct);
        if (!versions.IsSuccess)
            return FromResult(versions);

        var version = versions.Value!.SingleOrDefault(v => v.VersionNumber == versionNumber);
        if (version is null)
            throw new DocumentOperationException(404, "Versão não encontrada.");

        return FromResult(await service.ReviewAsync(
            id, version.Id, request with { Version = versionNumber }, ct));
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/approve")]
    public async Task<IActionResult> ApproveVersion(
        Guid id, Guid versionId,
        [FromBody] DocumentVersionApprovalRequest request, CancellationToken ct)
        => FromResult(await service.ApproveVersionAsync(id, versionId, request, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/reject")]
    public async Task<IActionResult> RejectVersion(
        Guid id, Guid versionId,
        [FromBody] DocumentVersionRejectionRequest request, CancellationToken ct)
        => FromResult(await service.RejectVersionAsync(id, versionId, request, ct));

    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid versionId, CancellationToken ct)
    {
        var result = await service.DownloadVersionAsync(id, versionId, ct);
        if (!result.IsSuccess)
            return Failure(404, "FILE_NOT_FOUND", result.Error!);

        return ProtectedFile(result.Value!);
    }

    [HttpGet("{id:guid}/files/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid id, Guid fileId, CancellationToken ct)
    {
        var result = await service.DownloadFileAsync(id, fileId, ct);
        if (!result.IsSuccess)
            return Failure(404, "FILE_NOT_FOUND", result.Error!);

        return ProtectedFile(result.Value!);
    }

    [HttpPost("{id:guid}/access-grants")]
    public async Task<IActionResult> Grant(
        Guid id, [FromBody] DocumentGrantRequest request, CancellationToken ct)
    {
        var result = await service.GrantAsync(id, request, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FromResult(result);
    }

    [HttpPost("{id:guid}/access-grants/{grantId:guid}/revoke")]
    public async Task<IActionResult> RevokeGrant(
        Guid id, Guid grantId, [FromQuery] int expectedVersion, CancellationToken ct)
    {
        await service.RevokeGrantAsync(id, grantId, expectedVersion, ct);
        return NoContent();
    }

    // Arquivos protegidos nunca devem ser cacheados nem reinterpretados pelo navegador.
    private IActionResult ProtectedFile(FileDownloadResult file)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.MimeType, file.FileName);
    }
}
