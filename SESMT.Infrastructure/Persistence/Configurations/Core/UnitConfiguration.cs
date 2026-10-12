using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("unidade", schema: "core");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.CompanyId).HasColumnName("empresa_id").IsRequired();

        builder.Property(u => u.Code)
            .HasColumnName("codigo")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.Name)
            .HasColumnName("nome")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.Type)
            .HasColumnName("tipo")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.CEP).HasColumnName("cep").HasMaxLength(8);
        builder.Property(u => u.Address).HasColumnName("logradouro").HasMaxLength(200);
        builder.Property(u => u.Number).HasColumnName("numero").HasMaxLength(20);
        builder.Property(u => u.Complement).HasColumnName("complemento").HasMaxLength(100);
        builder.Property(u => u.Neighborhood).HasColumnName("bairro").HasMaxLength(100);
        builder.Property(u => u.City).HasColumnName("cidade").HasMaxLength(120);
        builder.Property(u => u.State).HasColumnName("uf").HasMaxLength(2);

        MapAuditFields(builder);

        builder.HasIndex(u => new { u.CompanyId, u.Code })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_unidade_empresa_codigo");
    }

    private static void MapAuditFields(EntityTypeBuilder<Unit> b)
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