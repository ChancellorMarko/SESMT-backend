using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Docs;

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("arquivo_objeto", schema: "docs");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.StorageProvider)
            .HasColumnName("storage_provider")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.Bucket)
            .HasColumnName("bucket")
            .HasMaxLength(120);

        builder.Property(f => f.ObjectKey)
            .HasColumnName("object_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.FileName)
            .HasColumnName("nome_original")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Size)
            .HasColumnName("tamanho_bytes")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(f => f.FileHash)
            .HasColumnName("hash_sha256")
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(f => f.IsEncrypted)
            .HasColumnName("criptografado")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(f => f.AntivirusScanResult)
            .HasColumnName("antivirus_status")
            .HasMaxLength(20)
            .IsRequired();

        MapAuditFields(builder);

        builder.HasIndex(f => f.FileHash).HasDatabaseName("ix_arquivo_hash");
    }

    private static void MapAuditFields(EntityTypeBuilder<StoredFile> b)
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