using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;

namespace SESMT.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRefreshSessionService, RefreshSessionService>();
        services.AddScoped<ILogoutService, LogoutService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        return services;
    }
}
