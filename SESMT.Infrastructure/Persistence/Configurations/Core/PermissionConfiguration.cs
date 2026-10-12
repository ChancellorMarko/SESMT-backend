using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissao", schema: "core");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.Resource)
            .HasColumnName("recurso")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(p => p.Action)
            .HasColumnName("acao")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("descricao")
            .HasColumnType("text")
            .IsRequired();

        MapAuditFields(builder);

        builder.HasIndex(p => new { p.Resource, p.Action })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_permissao_recurso_acao");
    }

    private static void MapAuditFields(EntityTypeBuilder<Permission> b)
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