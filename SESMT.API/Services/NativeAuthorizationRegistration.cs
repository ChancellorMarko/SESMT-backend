using SESMT.Application.Modules.Organization.Interfaces;
using SESMT.Application.Modules.Organization.Services;
using SESMT.Application.Modules.Workforce.Interfaces;
using SESMT.Application.Modules.Workforce.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SESMT.API.Services;

public static class NativeAuthorizationRegistration
{
    // Called after AddApplication/AddInfrastructure. Concrete legacy implementations are
    // internal delegates; every native controller resolves an authorized interface.
    public static IServiceCollection AddNativeResourceAuthorization(this IServiceCollection services)
    {
        services.AddScoped<NativeResourceAccess>();
        services.AddScoped<OrganizationEditStore>();
        services.AddScoped<OrganizationWritePreconditionFilter>();
        services.AddScoped<TeamManagementContextService>();
        services.AddScoped<WorkerAccountBindingService>();
        services.AddScoped<WorkerService>();
        services.AddScoped<WorkerHistoryService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<UnitService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<PositionService>();
        services.AddScoped<TeamService>();
        services.Replace(ServiceDescriptor.Scoped<IWorkerService, AuthorizedWorkerService>());
        services.Replace(ServiceDescriptor.Scoped<IWorkerHistoryService, AuthorizedWorkerHistoryService>());
        services.Replace(ServiceDescriptor.Scoped<ICompanyService, AuthorizedCompanyService>());
        services.Replace(ServiceDescriptor.Scoped<IUnitService, AuthorizedUnitService>());
        services.Replace(ServiceDescriptor.Scoped<IDepartmentService, AuthorizedDepartmentService>());
        services.Replace(ServiceDescriptor.Scoped<IPositionService, AuthorizedPositionService>());
        services.Replace(ServiceDescriptor.Scoped<ITeamService, AuthorizedTeamService>());
        return services;
    }
}
