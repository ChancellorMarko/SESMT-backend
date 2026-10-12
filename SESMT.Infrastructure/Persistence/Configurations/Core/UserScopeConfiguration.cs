using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class UserScopeConfiguration : IEntityTypeConfiguration<UserScope>
{
    public void Configure(EntityTypeBuilder<UserScope> builder)
    {
        builder.ToTable("usuario_escopo", schema: "core");

        builder.HasKey(us => us.Id);
        builder.Property(us => us.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(us => us.UserId).HasColumnName("usuario_id").IsRequired();

        builder.Property(us => us.ScopeType)
            .HasColumnName("tipo_escopo")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(us => us.ReferenceId).HasColumnName("referencia_id");

        MapAuditFields(builder);

        builder.HasIndex(us => new { us.UserId, us.ScopeType, us.ReferenceId })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_usuario_escopo");
    }

    private static void MapAuditFields(EntityTypeBuilder<UserScope> b)
    {
        b.Property(x => x.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        b.Property(x => x.CreatedBy).HasColumnName("criado_por");
        b.Property(x => x.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        b.Property(x => x.UpdatedBy).HasColumnName("atualizado_por");
        b.Property(x => x.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        b.Property(x => x.DeletedBy).HasColumnName("excluido_por");
    }
}