using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("perfil_permissao", schema: "core");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(rp => rp.RoleId)
            .HasColumnName("perfil_id")
            .IsRequired();

        builder.Property(rp => rp.PermissionId)
            .HasColumnName("permissao_id")
            .IsRequired();

        builder.Property(rp => rp.Active)
            .HasColumnName("ativo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(rp => rp.CreatedAt)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(rp => rp.CreatedBy)
            .HasColumnName("criado_por");

        builder.Property(rp => rp.UpdatedAt)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz");

        builder.Property(rp => rp.UpdatedBy)
            .HasColumnName("atualizado_por");

        builder.Property(rp => rp.DeletedAt)
            .HasColumnName("excluido_em")
            .HasColumnType("timestamptz");

        builder.Property(rp => rp.DeletedBy)
            .HasColumnName("excluido_por");

        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_perfil_permissao");
    }
}