using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Services;
using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Services;
using SESMT.Application.Modules.Workforce.Interfaces;
using SESMT.Application.Modules.Workforce.Services;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Application.Modules.WorkPermits.Services;

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
        services.AddScoped<IWorkPermitService, WorkPermitService>();

        // Module Organization services
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IPositionService, PositionService>();

        // Module Workforce services
        services.AddScoped<IWorkerService, WorkerService>();
        services.AddScoped<IWorkerHistoryService, WorkerHistoryService>();

        return services;
    }
}
