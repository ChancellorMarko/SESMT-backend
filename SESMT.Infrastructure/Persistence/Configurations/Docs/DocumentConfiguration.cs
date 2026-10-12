using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Docs;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documento", schema: "docs");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.DocumentTypeId).HasColumnName("tipo_id").IsRequired();
        builder.Property(d => d.CompanyId).HasColumnName("empresa_id").IsRequired();
        builder.Property(d => d.UnitId).HasColumnName("unidade_id");
        builder.Property(d => d.DepartmentId).HasColumnName("setor_id");
        builder.Property(d => d.OwnerId).HasColumnName("proprietario_id").IsRequired();

        builder.Property(d => d.Code)
            .HasColumnName("codigo")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(d => d.Title)
            .HasColumnName("titulo")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.Description).HasColumnName("descricao").HasColumnType("text");

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.SecrecyLevel)
            .HasColumnName("classificacao_acesso")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.IssueDate).HasColumnName("data_emissao").HasColumnType("date").IsRequired();
        builder.Property(d => d.ValidFromDate).HasColumnName("data_vigencia_inicio").HasColumnType("date");
        builder.Property(d => d.ValidUntilDate).HasColumnName("data_vigencia_fim").HasColumnType("date");

        builder.Property(d => d.Keywords)
            .HasColumnName("palavras_chave")
            .HasColumnType("text[]");

        builder.Property(d => d.Metadata)
            .HasColumnName("metadados")
            .HasColumnType("jsonb")
            .IsRequired();

        MapAuditFields(builder);

        builder.HasIndex(d => new { d.DocumentTypeId, d.Code, d.UnitId })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_documento_tipo_codigo_unidade");

        builder.HasIndex(d => d.ValidUntilDate)
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_documento_vigencia");

        builder.HasIndex(d => d.Status)
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_documento_status");

        builder.HasIndex(d => d.Keywords)
            .HasMethod("GIN")
            .HasDatabaseName("ix_documento_palavras_chave");

        builder.HasIndex(d => d.Metadata)
            .HasMethod("GIN")
            .HasDatabaseName("ix_documento_metadados");
    }

    private static void MapAuditFields(EntityTypeBuilder<Document> b)
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