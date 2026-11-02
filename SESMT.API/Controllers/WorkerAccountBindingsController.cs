using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

[ApiController, Authorize, Route("workers/{id:guid}/account-binding")]
public sealed class WorkerAccountBindingsController(WorkerAccountBindingService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) => Ok(await service.ReadAsync(id, ct));
    [HttpGet("candidates")]
    public async Task<IActionResult> Search(Guid id, [FromQuery] string? q, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.SearchAsync(id, q, page, ct));
    [HttpGet("candidates/{userId:guid}")]
    public async Task<IActionResult> Candidate(Guid id, Guid userId, CancellationToken ct) => Ok(await service.CandidateAsync(id, userId, ct));
    [HttpPut]
    public async Task<IActionResult> Link(Guid id, [FromBody] LinkWorkerAccountRequest request, CancellationToken ct)
        => Ok(await service.LinkAsync(id, request, ct));
}
