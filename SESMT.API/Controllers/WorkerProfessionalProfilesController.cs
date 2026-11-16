using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; rules live in <see cref="WorkerProfessionalProfileService"/>.</summary>
[ApiController, Authorize, Route("workers/{id:guid}/professional-profile")]
public sealed class WorkerProfessionalProfilesController(WorkerProfessionalProfileService profiles) : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> Read(Guid id, CancellationToken ct) => Respond(() => profiles.ReadAsync(id, ct));
    [HttpPatch]
    public Task<IActionResult> Update(Guid id, WorkerProfessionalRequest request, CancellationToken ct) => Respond(() => profiles.UpdateAsync(id, request, ct));
}
