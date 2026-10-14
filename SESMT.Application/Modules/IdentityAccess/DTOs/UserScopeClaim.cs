namespace SESMT.Application.Modules.IdentityAccess.DTOs;

public sealed record UserScopeClaim(
    string ScopeType,
    Guid? ReferenceId);