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
using SESMT.Domain.Modules.Documents.Repositories;
using SESMT.Infrastructure.Persistence.Repositories.Documents;
using SESMT.Infrastructure.Storage;
using SESMT.Application.Modules.Dashboard;
using SESMT.Application.Modules.Notifications;
using SESMT.Infrastructure.Persistence.Repositories;

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
        services.AddScoped<IOperationalDesignationRepository, OperationalDesignationRepository>();

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

        // ─── Documentos ───────────────────────────────────────────────────
        // Repositórios de Documents
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentTypeRepository, DocumentTypeRepository>();
        services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
        services.AddScoped<SESMT.Application.Modules.Documents.Interfaces.IDocumentAccessPolicy, DocumentAccessPolicy>();
        services.AddScoped<SESMT.Application.Modules.Documents.Interfaces.IDocumentSearchRepository, DocumentSearchRepository>();
        services.AddScoped<SESMT.Application.Modules.Documents.Interfaces.IDocumentVersionWorkflow, DocumentVersionWorkflow>();
        var documentFiles = configuration.GetSection(SESMT.Application.Modules.Documents.DTOs.DocumentFileOptions.SectionName)
            .Get<SESMT.Application.Modules.Documents.DTOs.DocumentFileOptions>() ?? new SESMT.Application.Modules.Documents.DTOs.DocumentFileOptions();
        if (documentFiles.MaxSizeBytes is <= 0 or > 50 * 1024 * 1024 || documentFiles.AllowedMimeTypes is not { Length: > 0 }
            || documentFiles.AllowedMimeTypes.Any(type => type is not ("application/pdf" or "image/png" or "image/jpeg")))
            throw new InvalidOperationException("Documents:Files deve definir limite de até 50 MiB e tipos PDF, PNG ou JPEG.");
        services.AddSingleton(documentFiles);
        var documentQuery = configuration.GetSection(SESMT.Application.Modules.Documents.DTOs.DocumentQueryOptions.SectionName)
            .Get<SESMT.Application.Modules.Documents.DTOs.DocumentQueryOptions>() ?? new SESMT.Application.Modules.Documents.DTOs.DocumentQueryOptions();
        if (documentQuery.MaxPageSize is < 1 or > 200) throw new InvalidOperationException("Documents:Search:MaxPageSize deve estar entre 1 e 200.");
        services.AddSingleton(documentQuery);

        // Storage local
        services.Configure<LocalStorageOptions>(configuration.GetSection(LocalStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // DbContext com interceptor
        services.AddDbContext<SesmtDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(conn, npg =>
                npg.MigrationsAssembly(typeof(SesmtDbContext).Assembly.FullName));

            opt.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        // Repositório de auditoria
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<SESMT.Application.Modules.Appointments.IAppointmentRepository, AppointmentRepository>();
        services.AddSingleton(configuration.GetSection("Appointments").Get<SESMT.Application.Modules.Appointments.AppointmentOptions>()
            ?? new SESMT.Application.Modules.Appointments.AppointmentOptions());

        return services;
    }
}
