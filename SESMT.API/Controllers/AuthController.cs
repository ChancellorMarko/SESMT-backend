using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Constants;

namespace SESMT.API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ApiControllerBase
{
    private const string RefreshCookieName = "sesmt_refresh";
    private const string CsrfCookieName = "sesmt_csrf";
    
    // ⭐ Caminho alterado para cobrir /refresh e /logout
    private const string RefreshCookiePath = "/api/v1/auth";
    private const string LegacyRefreshCookiePath = "/api/v1/auth/refresh"; // Para limpar o antigo
    private const string CsrfCookiePath = "/api/v1/auth";

    private readonly ILoginService _loginService;
    private readonly IRefreshSessionService _refreshSessionService;
    private readonly ILogoutService _logoutService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IUserAuthenticationRepository _authRepository;

    public AuthController(
        ILoginService loginService,
        IRefreshSessionService refreshSessionService,
        ILogoutService logoutService,
        IPasswordResetService passwordResetService,
        IUserAuthenticationRepository authRepository)
    {
        _loginService = loginService;
        _refreshSessionService = refreshSessionService;
        _logoutService = logoutService;
        _passwordResetService = passwordResetService;
        _authRepository = authRepository;
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
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (!Guid.TryParse(userIdStr, out var userId))
            return Failure(401, "INVALID_CLAIMS", "Não foi possível identificar o usuário autenticado.");

        var authData = await _authRepository.GetByIdAsync(userId, ct);
        if (authData is null)
            return Failure(404, "USER_NOT_FOUND", "Usuário não encontrado.");

        var user = authData.User;

        // Mapeando os escopos para o formato que o front espera
        var companyIds = authData.Scopes.Where(s => s.ScopeType == ScopeType.EMPRESA && s.ReferenceId.HasValue).Select(s => s.ReferenceId!.Value).ToList();
        var unitIds = authData.Scopes.Where(s => s.ScopeType == ScopeType.UNIDADE && s.ReferenceId.HasValue).Select(s => s.ReferenceId!.Value).ToList();
        var sectorIds = authData.Scopes.Where(s => s.ScopeType == ScopeType.SETOR && s.ReferenceId.HasValue).Select(s => s.ReferenceId!.Value).ToList();
        
        // O token JWT expira em "exp" (Unix timestamp)
        var expClaim = User.FindFirstValue("exp");
        var accessExpiresAt = expClaim != null && long.TryParse(expClaim, out var expUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(expUnix)
            : DateTimeOffset.UtcNow.AddMinutes(15); // Fallback

        return Ok(new
        {
            user = new
            {
                id = user.Id,
                displayName = user.Name,
                email = user.Email.Value
            },
            roles = authData.Roles,
            permissions = Array.Empty<string>(), // Vazio por enquanto, conforme feedback
            scope = new
            {
                companyIds,
                unitIds,
                sectorIds,
                teamIds = Array.Empty<Guid>(), // Não existe escopo de "Team" na entidade ainda
                ownOnly = authData.Roles.Contains("TRABALHADOR") // Se for trabalhador, provavelmente é ownOnly
            },
            accessExpiresAt = accessExpiresAt.ToString("O")
        });
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
            return Failure(status, code, "Não foi possível autenticar. Verifique suas credenciais.");
        }

        var response = result.Response!;

        // ⭐ Limpa o cookie antigo com path /refresh
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });

        // Seta o novo cookie com path /auth (cobre /refresh e /logout)
        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            MaxAge = TimeSpan.FromDays(7),
        });

        // Gera também o CSRF
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
            // Se falhou, limpa os cookies antigos e novos
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
            return Failure(401, "SESSION_EXPIRED", "Sessão expirada. Faça login novamente.");
        }

        var response = result.Response!;

        // Limpa o legado novamente caso o usuário estivesse com o cookie antigo
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });
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
            var result = await _logoutService.ExecuteAsync(new LogoutRequest(refreshToken), ct);
            
            // ⭐ Se a revogação falhar no banco, não mentimos pro frontend
            if (!result.Succeeded)
            {
                return Failure(500, "LOGOUT_FAILED", "Não foi possível revogar a sessão no servidor.");
            }
        }

        // Limpa o cookie novo e o antigo, para garantir a migração
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
        
        Response.Cookies.Delete(CsrfCookieName, new CookieOptions { Path = CsrfCookiePath });

        return NoContent();
    }
}
