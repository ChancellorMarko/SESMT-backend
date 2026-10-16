using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Services;

namespace SESMT.Application;

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

        // Module Organization services
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IUnitService, UnitService>();

        return services;
    }
}
