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

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> ScopeIds,
    bool Active);
