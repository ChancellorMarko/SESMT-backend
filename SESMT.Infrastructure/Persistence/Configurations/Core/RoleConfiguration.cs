using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("perfil", schema: "core");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Code)
            .HasColumnName("codigo")
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_perfil_codigo");

        builder.Property(x => x.Name)
            .HasColumnName("nome")
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("descricao")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Active)
            .HasColumnName("ativo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("criado_por");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedBy)
            .HasColumnName("atualizado_por");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("excluido_em")
            .HasColumnType("timestamptz");

        builder.Property(x => x.DeletedBy)
            .HasColumnName("excluido_por");
    }
}