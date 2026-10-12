using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Docs;

public class DocumentLinkConfiguration : IEntityTypeConfiguration<DocumentLink>
{
    public void Configure(EntityTypeBuilder<DocumentLink> builder)
    {
        builder.ToTable("documento_vinculo", schema: "docs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(l => l.DocumentId).HasColumnName("documento_id").IsRequired();

        builder.Property(l => l.EntityType)
            .HasColumnName("entidade_tipo")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.EntityId).HasColumnName("entidade_id").IsRequired();

        MapAuditFields(builder);

        builder.HasIndex(l => new { l.DocumentId, l.EntityType, l.EntityId })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_documento_vinculo");

        builder.HasIndex(l => new { l.EntityType, l.EntityId })
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_documento_vinculo_entidade");
    }

    private static void MapAuditFields(EntityTypeBuilder<DocumentLink> b)
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