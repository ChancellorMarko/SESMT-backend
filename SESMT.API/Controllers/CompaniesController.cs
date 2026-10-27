using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.Organization.DTOs;
using SESMT.Application.Modules.Organization.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("companies")]
[Authorize]
public sealed class CompaniesController : ApiControllerBase
{
    private readonly ICompanyService _companyService;
    public CompaniesController(ICompanyService companyService) => _companyService = companyService;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CompanyCreateRequest request, CancellationToken ct)
    {
        var result = await _companyService.CreateAsync(request, ct);
        return CreatedFromResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
        => FromResult(await _companyService.ListAsync(pageSize, pageNumber, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => FromResult(await _companyService.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] CompanyCreateRequest request, CancellationToken ct)
        => FromResult(await _companyService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => NoContentFromResult(await _companyService.DeactivateAsync(id, ct));
}
