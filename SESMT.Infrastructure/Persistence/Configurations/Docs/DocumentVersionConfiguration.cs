using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Docs;

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("documento_versao", schema: "docs");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.DocumentId).HasColumnName("documento_id").IsRequired();
        builder.Property(v => v.StoredFileId).HasColumnName("arquivo_id").IsRequired();
        builder.Property(v => v.AuthorId).HasColumnName("autor_id").IsRequired();
        builder.Property(v => v.ApproverId).HasColumnName("aprovador_id");

        builder.Property(v => v.VersionNumber)
            .HasColumnName("numero_versao")
            .IsRequired();

        builder.Property(v => v.FileHash)
            .HasColumnName("hash_arquivo")
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(v => v.Justification).HasColumnName("justificativa").HasColumnType("text");

        builder.Property(v => v.State)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.IsCurrent)
            .HasColumnName("vigente")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(v => v.ApprovedAt)
            .HasColumnName("data_aprovacao")
            .HasColumnType("timestamptz");

        MapAuditFields(builder);

        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_documento_versao_numero");

        builder.HasIndex(v => v.DocumentId)
            .IsUnique()
            .HasFilter("vigente = true AND excluido_em IS NULL")
            .HasDatabaseName("ux_documento_versao_vigente");
    }

    private static void MapAuditFields(EntityTypeBuilder<DocumentVersion> b)
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