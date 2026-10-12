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

        builder.Property(x => x.Name)
            .HasColumnName("nome")
            .HasColumnType("varchar(200)");

        builder.Property(x => x.Description)
            .HasColumnName("descricao")
            .HasColumnType("text");

        builder.Property(x => x.Code)
            .HasColumnName("codigo")
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("excluido_em")
            .HasColumnType("timestamptz");

        builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName("ux_perfil_codigo");
    }
}