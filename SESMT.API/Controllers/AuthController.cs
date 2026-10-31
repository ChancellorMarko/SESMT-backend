using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Constants;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ApiControllerBase
{
    private const string RefreshCookieName = "sesmt_refresh";
    private const string CsrfCookieName = "sesmt_csrf";
    private const string RefreshCookiePath = "/api/v1/auth";
    private const string LegacyRefreshCookiePath = "/api/v1/auth/refresh";
    private const string CsrfCookiePath = "/api/v1/auth";

    private readonly ILoginService _loginService;
    private readonly IRefreshSessionService _refreshSessionService;
    private readonly ILogoutService _logoutService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IUserAuthenticationRepository _authRepository;
    private readonly SesmtDbContext _db;   // ⭐ NOVO

    public AuthController(
        ILoginService loginService,
        IRefreshSessionService refreshSessionService,
        ILogoutService logoutService,
        IPasswordResetService passwordResetService,
        IUserAuthenticationRepository authRepository,
        SesmtDbContext db)   // ⭐ NOVO
    {
        _loginService = loginService;
        _refreshSessionService = refreshSessionService;
        _logoutService = logoutService;
        _passwordResetService = passwordResetService;
        _authRepository = authRepository;
        _db = db;
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

        // ⭐ 1) Permissões reais: resource:action de todos os perfis ativos
        var permissions = await (
            from userRole in _db.UserRoles
            join rolePermission in _db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in _db.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId
                && userRole.Active && userRole.DeletedAt == null
                && rolePermission.Active && rolePermission.DeletedAt == null
                && permission.Active && permission.DeletedAt == null
            select permission.Resource + ":" + permission.Action)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync(ct);

        // Escopos explícitos do usuário
        var companyIds = authData.Scopes
            .Where(s => s.ScopeType == ScopeType.EMPRESA && s.ReferenceId.HasValue)
            .Select(s => s.ReferenceId!.Value)
            .ToList();

        var unitIds = authData.Scopes
            .Where(s => s.ScopeType == ScopeType.UNIDADE && s.ReferenceId.HasValue)
            .Select(s => s.ReferenceId!.Value)
            .ToList();

        var sectorIds = authData.Scopes
            .Where(s => s.ScopeType == ScopeType.SETOR && s.ReferenceId.HasValue)
            .Select(s => s.ReferenceId!.Value)
            .ToList();

        // ⭐ 2) Fallback: ADMIN global sem UserScope enxerga todas as empresas ativas
        var isGlobalAdmin = authData.Roles.Contains("ADMIN") && companyIds.Count == 0;
        if (isGlobalAdmin)
        {
            if (companyIds.Count == 0)
            {
                companyIds = await _db.Companies
                    .Where(c => c.DeletedAt == null)
                    .Select(c => c.Id)
                    .ToListAsync(ct);
            }

            if (unitIds.Count == 0)
            {
                unitIds = await _db.Units
                    .Where(u => u.DeletedAt == null && u.Active)
                    .Select(u => u.Id)
                    .ToListAsync(ct);
            }

            if (sectorIds.Count == 0)
            {
                sectorIds = await _db.Departments
                    .Where(d => d.DeletedAt == null && d.Active)
                    .Select(d => d.Id)
                    .ToListAsync(ct);
            }
        }

        // Expiração do access token
        var expClaim = User.FindFirstValue("exp");
        var accessExpiresAt = expClaim != null && long.TryParse(expClaim, out var expUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(expUnix)
            : DateTimeOffset.UtcNow.AddMinutes(15);

        return Ok(new
        {
            user = new
            {
                id = user.Id,
                displayName = user.Name,
                email = user.Email.Value
            },
            roles = authData.Roles,
            permissions,                          // ← agora vem do banco
            scope = new
            {
                companyIds,
                unitIds,
                sectorIds,
                teamIds = Array.Empty<Guid>(),
                ownOnly = authData.Roles.Contains("TRABALHADOR")
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
                "mfa_required" => (403, "MFA_REQUIRED"),
                "account_blocked" => (401, "ACCOUNT_BLOCKED"),
                _ => (401, "INVALID_CREDENTIALS"),
            };
            return Failure(status, code, "Não foi possível autenticar. Verifique suas credenciais.");
        }

        var response = result.Response!;

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });

        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            MaxAge = TimeSpan.FromDays(7),
        });

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
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
            return Failure(401, "SESSION_EXPIRED", "Sessão expirada. Faça login novamente.");
        }

        var response = result.Response!;

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

            if (!result.Succeeded)
            {
                return Failure(500, "LOGOUT_FAILED", "Não foi possível revogar a sessão no servidor.");
            }
        }

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = LegacyRefreshCookiePath });
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
        Response.Cookies.Delete(CsrfCookieName, new CookieOptions { Path = CsrfCookiePath });

        return NoContent();
    }
}
