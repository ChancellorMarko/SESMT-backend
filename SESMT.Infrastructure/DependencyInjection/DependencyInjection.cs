using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Infrastructure.Identity;
using SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Infrastructure.Persistence.Repositories.WorkPermits;
using SESMT.Infrastructure.WorkPermits;

namespace SESMT.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IUserAuthenticationRepository, UserAuthenticationRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IWorkPermitRepository, WorkPermitRepository>();
        services.AddScoped<IWorkPermitReadinessService, FailClosedWorkPermitReadinessService>();

        return services;
    }
}
