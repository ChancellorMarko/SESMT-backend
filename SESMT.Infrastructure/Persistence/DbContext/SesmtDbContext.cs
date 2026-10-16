using Microsoft.EntityFrameworkCore;
using SESMT.Domain.Modules.Administration.Entities;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Notifications.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence;

public class SesmtDbContext : DbContext
{
    public SesmtDbContext(DbContextOptions<SesmtDbContext> options) : base(options) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureWorkPermitHistoryIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureWorkPermitHistoryIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Módulo IdentityAccess
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Módulo Organization
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Team> Teams => Set<Team>();

    // Módulo Workforce
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<WorkerHistory> WorkerHistories => Set<WorkerHistory>();

    // Módulo WorkPermits
    public DbSet<WorkPermit> WorkPermits => Set<WorkPermit>();
    public DbSet<WorkPermitTransition> WorkPermitTransitions => Set<WorkPermitTransition>();
    public DbSet<WorkPermitAppointment> WorkPermitAppointments => Set<WorkPermitAppointment>();
    public DbSet<WorkPermitParticipant> WorkPermitParticipants => Set<WorkPermitParticipant>();
    public DbSet<WorkPermitDocumentRequirement> WorkPermitDocumentRequirements => Set<WorkPermitDocumentRequirement>();

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

    private void EnsureWorkPermitHistoryIsAppendOnly()
    {
        if (ChangeTracker.Entries<WorkPermitTransition>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("O histórico de permissões de trabalho é somente de inclusão.");
        }
    }
}
