using SESMT.Application.Common.Interfaces;

namespace SESMT.Tests.Unit.Identity;

/// <summary>Authenticated actor for unit tests that call services directly.</summary>
internal sealed class FixedCurrentUser(Guid id) : ICurrentUserService
{
    public Guid? UserId => id;
    public string? Email => null;
    public string? Name => null;
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string? TraceId => null;
    public bool IsAuthenticated => true;
}
