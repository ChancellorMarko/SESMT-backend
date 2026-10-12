using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Administration.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.ToTable("parametro_sistema", schema: "core");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.Key)
            .HasColumnName("chave")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Value)
            .HasColumnName("valor")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("descricao")
            .HasColumnType("text");

        builder.Property(p => p.NormativeReference)
            .HasColumnName("fonte_normativa")
            .HasMaxLength(255);

        builder.Property(p => p.Version)
            .HasColumnName("versao")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(p => p.ValidityIncialDate)
            .HasColumnName("vigencia_inicio")
            .HasColumnType("date");

        builder.Property(p => p.ValidityFinalDate)
            .HasColumnName("vigencia_fim")
            .HasColumnType("date");

        MapAuditFields(builder);

        builder.HasIndex(p => p.Key)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_parametro_chave");
    }

    private static void MapAuditFields(EntityTypeBuilder<SystemParameter> b)
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