using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Docs;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("documento_tipo", schema: "docs");

        builder.HasKey(dt => dt.Id);
        builder.Property(dt => dt.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(dt => dt.Code)
            .HasColumnName("codigo")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(dt => dt.Name)
            .HasColumnName("nome")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(dt => dt.Description)
            .HasColumnName("descricao")
            .HasColumnType("text");

        builder.Property(dt => dt.Category)
            .HasColumnName("categoria")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(dt => dt.SecrecyLevel)
            .HasColumnName("sigilo")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(dt => dt.RequiresSignature)
            .HasColumnName("requer_assinatura")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(dt => dt.RequiresApproval)
            .HasColumnName("requer_aprovacao")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(dt => dt.PeriodicityInDays).HasColumnName("periodicidade_dias");
        builder.Property(dt => dt.RetentionPeriodInYears).HasColumnName("prazo_guarda_anos");

        builder.Property(dt => dt.MetadataSchema)
            .HasColumnName("metadados_schema")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(dt => dt.SchemaVersion)
            .HasColumnName("versao_schema")
            .HasDefaultValue(1)
            .IsRequired();

        MapAuditFields(builder);

        builder.HasIndex(dt => dt.Code)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_documento_tipo_codigo");
    }

    private static void MapAuditFields(EntityTypeBuilder<DocumentType> b)
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