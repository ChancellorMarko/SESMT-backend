using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Contracts.Errors;
using SESMT.Application.Modules.IdentityAccess.DTOs;
using SESMT.Application.Modules.IdentityAccess.Interfaces;

namespace SESMT.API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _passwordResetService.ResetAsync(request, cancellationToken);
        if (result.Succeeded)
            return NoContent();

        return BadRequest(new ErrorResponse(
            StatusCodes.Status400BadRequest,
            "Não foi possível redefinir a senha",
            "O token é inválido, expirou ou já foi utilizado. A senha deve ter pelo menos 12 caracteres."));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? Ok(new { userId = parsedUserId })
            : Unauthorized();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _loginService.ExecuteAsync(
            request,
            cancellationToken);

        if (result.Succeeded)
            return Ok(result.Response);

        return result.ErrorCode switch
        {
            "mfa_required" => StatusCode(
                StatusCodes.Status403Forbidden,
                new ErrorResponse(
                    StatusCodes.Status403Forbidden,
                    "Autenticação adicional necessária",
                    "Conclua a autenticação multifator para continuar.")),

            "account_blocked" => Unauthorized(
                new ErrorResponse(
                    StatusCodes.Status401Unauthorized,
                    "Autenticação não realizada",
                    "Não foi possível concluir a autenticação.")),

            _ => Unauthorized(
                new ErrorResponse(
                    StatusCodes.Status401Unauthorized,
                    "Credenciais inválidas",
                    "Não foi possível concluir a autenticação."))
        };
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _refreshSessionService.ExecuteAsync(
            request,
            cancellationToken);

        if (result.Succeeded)
            return Ok(result.Response);

        return Unauthorized(
            new ErrorResponse(
                StatusCodes.Status401Unauthorized,
                "Sessão inválida",
                result.ErrorCode switch
                {
                    "session_expired" =>
                        "A sessão expirou. Faça login novamente.",
                    "session_revoked" =>
                        "A sessão foi encerrada. Faça login novamente.",
                    _ =>
                        "Não foi possível renovar a sessão."
                }));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _logoutService.ExecuteAsync(
            request,
            cancellationToken);

        if (result.Succeeded)
            return NoContent();

        return Unauthorized(
            new ErrorResponse(
                StatusCodes.Status401Unauthorized,
                "Sessão inválida",
                "Não foi possível encerrar a sessão."));
    }
}
