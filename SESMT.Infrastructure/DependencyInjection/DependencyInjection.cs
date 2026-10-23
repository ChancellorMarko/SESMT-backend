using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SESMT.Application.Common;
using SESMT.Application.Common.Interfaces;
using SESMT.Application.Modules.IdentityAccess.Interfaces;
using SESMT.Application.Modules.WorkPermits.Interfaces;
using SESMT.Domain.Modules.Organization.Repositories;
using SESMT.Domain.Modules.Workforce.Repositories;
using SESMT.Infrastructure.Identity;
using SESMT.Infrastructure.Persistence;
using SESMT.Infrastructure.Persistence.Repositories.IdentityAccess;
using SESMT.Infrastructure.Persistence.Repositories.Organization;
using SESMT.Infrastructure.Persistence.Repositories.WorkPermits;
using SESMT.Infrastructure.Persistence.Repositories.Workforce;
using SESMT.Infrastructure.WorkPermits;
using SESMT.Infrastructure.Persistence.Interceptors;
using SESMT.Infrastructure.Persistence.Repositories.Audit;
using SESMT.Domain.Modules.Audit.Repositories;

namespace SESMT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── DbContext ──────────────────────────────────────────────────────
        var conn = configuration.GetConnectionString("Sesmt")
            ?? throw new InvalidOperationException(
                "Connection string 'Sesmt' não configurada.");

        services.AddDbContext<SesmtDbContext>(opt =>
            opt.UseNpgsql(conn, npg =>
                npg.MigrationsAssembly(typeof(SesmtDbContext).Assembly.FullName)));

        // ─── Unit of Work ───────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ─── Repositórios — Organization ────────────────────────────────────
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();

        // ─── Repositórios — Workforce ───────────────────────────────────────
        services.AddScoped<IWorkerRepository, WorkerRepository>();
        services.AddScoped<IWorkerHistoryRepository, WorkerHistoryRepository>();
        services.AddDataProtection();
        services.AddSingleton<ICpfProtectionService, CpfProtectionService>();

        // ─── Repositórios — IdentityAccess ──────────────────────────────────
        services.AddScoped<IUserAuthenticationRepository, UserAuthenticationRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();

        // ─── Repositórios — WorkPermits ─────────────────────────────────────
        services.AddScoped<IWorkPermitRepository, WorkPermitRepository>();

        // ─── Serviços de Identity (criptografia e JWT) ──────────────────────
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        // ─── Serviços de WorkPermits ────────────────────────────────────────
        services.AddScoped<IWorkPermitReadinessService, FailClosedWorkPermitReadinessService>();

        // ─── TimeProvider ───────────────────────────────────────────────────
        services.AddSingleton(TimeProvider.System);

        // ─── AuditLog ───────────────────────────────────────────────────
        // Registrar o interceptor (Scoped — usa ICurrentUserService que é Scoped)
        services.AddScoped<AuditInterceptor>();

        // DbContext com interceptor
        services.AddDbContext<SesmtDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(conn, npg =>
                npg.MigrationsAssembly(typeof(SesmtDbContext).Assembly.FullName));

            opt.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        // Repositório de auditoria
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        return services;
    }
}