using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Users;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; rules live in <see cref="UserManagementService"/>.</summary>
[ApiController]
[Authorize]
[Route("users")]
public sealed class UsersController(UserManagementService users) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { var created = (UserResponse)await users.CreateAsync(request, ct)!; return Created($"/api/v1/users/{created.Id}", created); }
        catch (ServiceRefusal refusal) { return Failure(refusal.Status, refusal.Code, refusal.Message); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(CancellationToken ct) => Respond(() => users.ListAsync(ct));

    [HttpPatch("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) => Respond(() => users.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/roles")]
    public Task<IActionResult> SetRoles(Guid id, [FromBody] UserRolesRequest request, CancellationToken ct) => Respond(() => users.SetRolesAsync(id, request, ct));

    [HttpPost("{id:guid}/scopes")]
    public Task<IActionResult> SetScopes(Guid id, [FromBody] UserScopesRequest request, CancellationToken ct) => Respond(() => users.SetScopesAsync(id, request, ct));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Respond(() => users.DeactivateAsync(id, ct), StatusCodes.Status204NoContent);

    /// <summary>Ends the account's sessions and issues a one-time reset token; the administrator never sees or sets the password.</summary>
    [HttpPost("{id:guid}/password-reset")]
    [ProducesResponseType(typeof(AdminPasswordResetResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> ResetPassword(Guid id, [FromBody] AdminPasswordResetRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Respond(() => users.ResetPasswordAsync(id, request, ct));
    }

    [HttpGet("management-context")]
    public Task<IActionResult> ManagementContext([FromQuery] string action, [FromQuery] Guid? userId, CancellationToken ct) => Respond(() => users.ManagementContextAsync(action, userId, ct));

    [HttpPut("{id:guid}/access")]
    public Task<IActionResult> SetAccess(Guid id, [FromBody] UserAccessRequest request, CancellationToken ct) => Respond(() => users.SetAccessAsync(id, request, ct));
}
