using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Docs;

public sealed class DocumentAccessGrantConfiguration : IEntityTypeConfiguration<DocumentAccessGrant>
{
    public void Configure(EntityTypeBuilder<DocumentAccessGrant> b)
    {
        b.ToTable("documento_concessao", "docs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.DocumentId).HasColumnName("documento_id");
        b.Property(x => x.UserId).HasColumnName("usuario_id");
        b.Property(x => x.GrantedBy).HasColumnName("concedido_por");
        b.Property(x => x.CanReview).HasColumnName("pode_conferir");
        b.Property(x => x.ExpiresAt).HasColumnName("expira_em");
        b.Property(x => x.RevokedAt).HasColumnName("revogado_em");
        b.Property(x => x.Version).HasColumnName("versao").IsConcurrencyToken();
        b.Property(x => x.Active).HasColumnName("ativo").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("criado_em");
        b.Property(x => x.CreatedBy).HasColumnName("criado_por");
        b.Property(x => x.UpdatedAt).HasColumnName("atualizado_em");
        b.Property(x => x.UpdatedBy).HasColumnName("atualizado_por");
        b.Property(x => x.DeletedAt).HasColumnName("excluido_em");
        b.Property(x => x.DeletedBy).HasColumnName("excluido_por");
        b.HasIndex(x => new { x.DocumentId, x.UserId, x.ExpiresAt });
        b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
