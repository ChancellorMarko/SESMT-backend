namespace SESMT.API.Contracts.Users;

public sealed record CreateUserRequest(
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> ScopeIds);

public sealed record UpdateUserRequest(
    string Name,
    string Email);

public sealed record UserRolesRequest(IReadOnlyList<string> Roles);

public sealed record UserScopesRequest(IReadOnlyList<Guid> ScopeIds);

public sealed record UserAccessRequest(IReadOnlyList<string> Roles, IReadOnlyList<Guid> ScopeIds, string ExpectedVersion, bool Confirmation);
public sealed record UserRoleOption(string Code, string Name, string Description);
public sealed record UserScopeOption(Guid Id, string Type, string Name, Guid CompanyId, string CompanyName);
public sealed record UserManagementContext(string Action, UserResponse? Target, string? AccessVersion,
    IReadOnlyList<UserRoleOption> Roles, IReadOnlyList<UserScopeOption> Scopes);
public sealed record UserAccessResponse(UserResponse User, string AccessVersion);

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> ScopeIds,
    bool Active);
