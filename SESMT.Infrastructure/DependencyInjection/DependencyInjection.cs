using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Infrastructure.Identity;
using SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;

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

        return services;
    }
}
