using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

/// <summary>Controller para Empresas (Companies).</summary>
[ApiController]
[Route("companies")]
[Authorize]
public sealed class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService) => _companyService = companyService;

    // ─── POST /companies ────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CompanyCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /companies ─────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await _companyService.ListAsync(pageSize, pageNumber, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapFailure(result.Error!);
    }

    // ─── GET /companies/{id} ────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.GetByIdAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Empresa não encontrada",
                result.Error!));
    }

    // ─── PATCH /companies/{id} ──────────────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CompanyCreateRequest updateData,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.UpdateAsync(id, updateData, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapFailure(result.Error!);
    }

    // ─── DELETE /companies/{id} (soft delete) ───────────────────────────────
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _companyService.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(new ErrorResponse(
                StatusCodes.Status404NotFound,
                "Empresa não encontrada",
                result.Error!));
    }

    // ─── Helper ─────────────────────────────────────────────────────────────
    private IActionResult MapFailure(string error)
    {
        var (status, title) = error switch
        {
            var e when e.Contains("não encontrad", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, "Recurso não encontrado"),

            var e when e.Contains("já existe", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("CNPJ", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status409Conflict, "Recurso duplicado"),

            var e when e.Contains("prestadora", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("organização", StringComparison.OrdinalIgnoreCase)
                   || e.Contains("não é permitido", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio não atendida"),

            _   => (StatusCodes.Status400BadRequest, "Dados inválidos")
        };

        return StatusCode(status, new ErrorResponse(status, title, error));
    }
}