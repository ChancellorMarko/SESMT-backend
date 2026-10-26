using System.Security.Claims;
using SESMT.Application.Common.Interfaces;

namespace SESMT.API.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContext;

    public CurrentUserService(IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContext.HttpContext?.User.FindFirstValue("sub")
                ?? _httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email =>
        _httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? Name =>
        _httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    public string? IpAddress
    {
        get
        {
            var forwarded = _httpContext.HttpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent =>
        _httpContext.HttpContext?.Request.Headers.UserAgent.ToString();

    public string? TraceId => _httpContext.HttpContext?.TraceIdentifier;

    public bool IsAuthenticated =>
        _httpContext.HttpContext?.User.Identity?.IsAuthenticated == true;
}
