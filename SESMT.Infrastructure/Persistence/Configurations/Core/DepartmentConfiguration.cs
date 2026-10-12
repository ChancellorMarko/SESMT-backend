using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("setor", schema: "core");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.UnitId).HasColumnName("unidade_id").IsRequired();

        builder.Property(d => d.Name)
            .HasColumnName("nome")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(d => d.Description).HasColumnName("descricao").HasColumnType("text");
        builder.Property(d => d.ResponsibleId).HasColumnName("responsavel_id");

        MapAuditFields(builder);

        builder.HasIndex(d => new { d.UnitId, d.Name })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_setor_unidade_nome");
    }

    private static void MapAuditFields(EntityTypeBuilder<Department> b)
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