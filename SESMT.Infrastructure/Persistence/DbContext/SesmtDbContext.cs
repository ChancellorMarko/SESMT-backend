using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Administration.Entities;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Notifications.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Infrastructure.Persistence;

public class SesmtDbContext : DbContext
{
    public SesmtDbContext(DbContextOptions<SesmtDbContext> options) : base(options) { }

    // Módulo IdentityAccess
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    // Módulo Organization
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();

    // Módulo Workforce
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<WorkerHistory> WorkerHistories => Set<WorkerHistory>();

    // Módulo Documents
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();

    // Módulo Notifications
    public DbSet<Alert> Alerts => Set<Alert>();

    // Módulo Administration
    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    // Módulo Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SesmtDbContext).Assembly);

        modelBuilder.HasDefaultSchema("core");

        base.OnModelCreating(modelBuilder);
    }
}