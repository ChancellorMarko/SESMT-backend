using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("usuario_perfil", schema: "core");

        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(ur => ur.UserId).HasColumnName("usuario_id").IsRequired();
        builder.Property(ur => ur.RoleId).HasColumnName("perfil_id").IsRequired();
        builder.Property(ur => ur.CompanyId).HasColumnName("empresa_id");

        MapAuditFields(builder);

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId, ur.CompanyId })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_usuario_perfil");
    }

    private static void MapAuditFields(EntityTypeBuilder<UserRole> b)
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