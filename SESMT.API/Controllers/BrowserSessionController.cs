using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Browser;
using SESMT.API.Services;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Constants;

namespace SESMT.API.Controllers;

/// <summary>Browser contract. Existing native API routes retain their original contracts.</summary>
[ApiController]
[Route("browser/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BrowserSessionController(
    ILoginService login,
    IRefreshSessionService refresh,
    ILogoutService logout,
    IBrowserIdentityReader identities,
    IAntiforgery antiforgery,
    IWebHostEnvironment environment) : ControllerBase
{
    public const string RefreshCookie = "ampara.refresh";
    public const string RefreshCookiePath = "/api/v1/browser/auth";

    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var principal = HttpContext.User;
        try
        {
            // The access token may appear or expire independently of the refresh cookie.
            // Bind CSRF to its protected cookie pair, not the transient bearer identity.
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { csrfToken = tokens.RequestToken });
        }
        finally { HttpContext.User = principal; }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var csrfError = await ValidateCsrfAsync();
        if (csrfError is not null) return csrfError;
        var result = await login.ExecuteAsync(request, ct);
        if (!result.Succeeded || result.Response is null)
            return result.ErrorCode == "mfa_required"
                ? Error(403, "MFA_REQUIRED", "Esta conta exige autenticação multifator. A integração desse fluxo ainda está pendente.")
                : Error(401, "INVALID_CREDENTIALS", "Não foi possível concluir a autenticação. Confira suas credenciais.");
        return await EstablishAsync(result.Response, ct);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var csrfError = await ValidateCsrfAsync();
        if (csrfError is not null) return csrfError;
        var token = Request.Cookies[RefreshCookie];
        if (string.IsNullOrWhiteSpace(token)) return Error(401, "SESSION_EXPIRED", "Entre novamente para continuar.");
        var result = await refresh.ExecuteAsync(new RefreshTokenRequest(token), ct);
        if (!result.Succeeded || result.Response is null)
        {
            DeleteRefreshCookie();
            return Error(401, "SESSION_EXPIRED", "A sessão expirou ou foi revogada. Entre novamente.");
        }
        return await EstablishAsync(result.Response, ct);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var csrfError = await ValidateCsrfAsync();
        if (csrfError is not null) return csrfError;
        var token = Request.Cookies[RefreshCookie];
        if (string.IsNullOrWhiteSpace(token))
        {
            DeleteRefreshCookie();
            return NoContent();
        }
        // Do not report revocation success when the database operation fails.
        var result = await logout.ExecuteAsync(new LogoutRequest(token), ct);
        if (!result.Succeeded)
            return Error(500, "LOGOUT_FAILED", "Não foi possível encerrar a sessão. Tente novamente.");
        DeleteRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("/browser/me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId)) return Error(401, "UNAUTHENTICATED", "Entre novamente para continuar.");
        var identity = await identities.FindAsync(userId, ct);
        var rejection = IdentityRejection(identity);
        if (rejection is not null) return rejection;
        if (!long.TryParse(User.FindFirstValue("exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var exp)
            || exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return Error(401, "TOKEN_EXPIRED", "Seu acesso expirou. Renove a sessão.");

        var data = identity!.Authentication;
        var ownOnly = IsWorkerOnly(data.Roles);
        return Ok(new BrowserMeResponse(
            new BrowserUserResponse(data.User.Id, data.User.Name, data.User.Email.Value),
            data.Roles,
            identity.Workspace?.Permissions ?? Array.Empty<string>(),
            new BrowserScopeResponse(
                identity.Workspace?.CompanyIds ?? ScopeIds(data.Scopes, ScopeType.EMPRESA),
                identity.Workspace?.UnitIds ?? ScopeIds(data.Scopes, ScopeType.UNIDADE),
                identity.Workspace?.SectorIds ?? ScopeIds(data.Scopes, ScopeType.SETOR), Array.Empty<Guid>(), ownOnly),
            identity.WorkerId,
            DateTimeOffset.FromUnixTimeSeconds(exp)));
    }

    private async Task<IActionResult> EstablishAsync(LoginResponse response, CancellationToken ct)
    {
        var identity = await identities.FindAsync(response.UserId, ct);
        var rejection = IdentityRejection(identity);
        if (rejection is not null)
        {
            // Login/refresh already persisted a session. Revoke it before refusing browser access.
            await logout.ExecuteAsync(new LogoutRequest(response.RefreshToken), ct);
            DeleteRefreshCookie();
            return rejection;
        }
        Response.Cookies.Append(RefreshCookie, response.RefreshToken, CookieOptions(response.RefreshTokenExpiresAtUtc));
        return Ok(new BrowserSessionResponse(response.AccessToken, "Bearer", response.ExpiresAtUtc));
    }

    private IActionResult? IdentityRejection(BrowserIdentity? identity)
    {
        if (identity is null) return Error(401, "UNAUTHENTICATED", "Esta conta não está disponível para acesso.");
        if (identity.Authentication.User.MfaEnabled)
            return Error(403, "MFA_REQUIRED", "Esta conta exige autenticação multifator. A integração desse fluxo ainda está pendente.");
        if (IsWorkerOnly(identity.Authentication.Roles) && (identity.WorkerId is null || identity.AmbiguousWorker))
            return Error(403, "WORKER_LINK_REQUIRED", "A conta precisa de um vínculo único com um trabalhador ativo. Solicite a configuração ao administrador.");
        return null;
    }

    private async Task<IActionResult?> ValidateCsrfAsync()
    {
        var principal = HttpContext.User;
        try
        {
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            await antiforgery.ValidateRequestAsync(HttpContext);
            return null;
        }
        catch (AntiforgeryValidationException)
        { return Error(403, "CSRF_INVALID", "Atualize a página e tente novamente."); }
        finally { HttpContext.User = principal; }
    }

    private CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict, Path = RefreshCookiePath, IsEssential = true, Expires = expires
    };
    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions());
    private ObjectResult Error(int status, string code, string message) =>
        StatusCode(status, new BrowserErrorResponse(code, message, HttpContext.TraceIdentifier));
    private static bool IsWorkerOnly(IReadOnlyCollection<string> roles) =>
        roles.Count > 0 && roles.All(role => role is "TRABALHADOR" or "WORKER");
    private static Guid[] ScopeIds(IEnumerable<UserScopeClaim> scopes, string kind) =>
        scopes.Where(scope => scope.ScopeType == kind && scope.ReferenceId is { } id && id != Guid.Empty)
            .Select(scope => scope.ReferenceId!.Value).Distinct().ToArray();
}
