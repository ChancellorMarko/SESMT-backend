using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ApiControllerBase
{
    private const string RefreshCookieName = "sesmt_refresh";
    private const string CsrfCookieName = "sesmt_csrf";
    private const string RefreshCookiePath = "/api/v1/auth/refresh";
    private const string CsrfCookiePath = "/api/v1/auth";

    private readonly ILoginService _loginService;
    private readonly IRefreshSessionService _refreshSessionService;
    private readonly ILogoutService _logoutService;
    private readonly IPasswordResetService _passwordResetService;

    public AuthController(
        ILoginService loginService,
        IRefreshSessionService refreshSessionService,
        ILogoutService logoutService,
        IPasswordResetService passwordResetService)
    {
        _loginService = loginService;
        _refreshSessionService = refreshSessionService;
        _logoutService = logoutService;
        _passwordResetService = passwordResetService;
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _passwordResetService.RequestAsync(request, cancellationToken);
        return Accepted(new
        {
            message = "Se o e-mail estiver cadastrado, você receberá instruções para redefinir a senha."
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _passwordResetService.ResetAsync(request, cancellationToken);

        if (result.Succeeded)
            return NoContent();

        return Failure(400, "INVALID_RESET_TOKEN",
            "O token é inválido, expirou ou já foi utilizado. A senha deve ter pelo menos 12 caracteres.");
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? Ok(new { userId = parsedUserId })
            : Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário autenticado.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _loginService.ExecuteAsync(request, ct);

        if (!result.Succeeded)
        {
            var (status, code) = result.ErrorCode switch
            {
                "mfa_required"    => (403, "MFA_REQUIRED"),
                "account_blocked" => (401, "ACCOUNT_BLOCKED"),
                _                 => (401, "INVALID_CREDENTIALS"),
            };
            return Failure(status, code,
                "Não foi possível autenticar. Verifique suas credenciais.");
        }

        var response = result.Response!;

        // ⭐ Refresh token vai apenas no cookie HttpOnly
        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            MaxAge = TimeSpan.FromDays(7),
        });

        // Gera também o CSRF (duplo-submit) para o /refresh
        var csrfToken = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(CsrfCookieName, csrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CsrfCookiePath,
        });

        return Ok(new
        {
            accessToken = response.AccessToken,
            expiresAtUtc = response.ExpiresAtUtc,
            userId = response.UserId,
            roles = response.Roles,
            scopes = response.Scopes,
        });
    }

    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var token = Guid.NewGuid().ToString("N");

        Response.Cookies.Append(CsrfCookieName, token, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CsrfCookiePath,
        });

        return Ok(new { csrfToken = token });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var csrfCookie = Request.Cookies[CsrfCookieName];
        var csrfHeader = Request.Headers["X-CSRF-Token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(csrfCookie)
            || string.IsNullOrWhiteSpace(csrfHeader)
            || !string.Equals(csrfCookie, csrfHeader, StringComparison.Ordinal))
        {
            return Failure(403, "CSRF_INVALID", "Requisição rejeitada.");
        }

        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Failure(401, "NO_REFRESH_TOKEN", "Sessão expirada.");

        var result = await _refreshSessionService.ExecuteAsync(
            new RefreshTokenRequest(refreshToken), ct);

        if (!result.Succeeded)
        {
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
            return Failure(401, "SESSION_EXPIRED", "Sessão expirada. Faça login novamente.");
        }

        var response = result.Response!;

        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            MaxAge = TimeSpan.FromDays(7),
        });

        return Ok(new
        {
            accessToken = response.AccessToken,
            expiresAtUtc = response.ExpiresAtUtc,
            userId = response.UserId,
            roles = response.Roles,
            scopes = response.Scopes,
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _logoutService.ExecuteAsync(new LogoutRequest(refreshToken), ct);
        }

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
        Response.Cookies.Delete(CsrfCookieName, new CookieOptions { Path = CsrfCookiePath });

        return NoContent();
    }
}
