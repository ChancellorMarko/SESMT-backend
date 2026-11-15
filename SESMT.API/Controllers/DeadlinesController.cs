using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; rules live in <see cref="DeadlineService"/>.</summary>
[ApiController, Authorize, Route("deadlines")]
public sealed class DeadlinesController(DeadlineService deadlines) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int days = 30, Guid? unitId = null, Guid? workerId = null, string? kind = null)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await deadlines.ListAsync(days, unitId, workerId, kind, ct));
    }
}
