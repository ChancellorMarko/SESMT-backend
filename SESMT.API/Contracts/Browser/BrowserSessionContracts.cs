namespace SESMT.API.Contracts.Browser;

public sealed record BrowserSessionResponse(string AccessToken, string TokenType, DateTimeOffset AccessExpiresAt);
public sealed record BrowserErrorResponse(string Code, string Message, string RequestId);
public sealed record BrowserUserResponse(Guid Id, string DisplayName, string Email);
public sealed record BrowserScopeResponse(
    IReadOnlyCollection<Guid> CompanyIds,
    IReadOnlyCollection<Guid> UnitIds,
    IReadOnlyCollection<Guid> SectorIds,
    IReadOnlyCollection<Guid> TeamIds,
    bool OwnOnly);
public sealed record BrowserMeResponse(
    BrowserUserResponse User,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    BrowserScopeResponse Scope,
    Guid? WorkerId,
    DateTimeOffset AccessExpiresAt);
