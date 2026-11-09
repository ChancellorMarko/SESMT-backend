using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Training.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.Documents.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class TrainingConfiguration : IEntityTypeConfiguration<TrainingRecord>
{
    public void Configure(EntityTypeBuilder<TrainingRecord> b)
    {
        b.ToTable("treinamento_registro", "core", t => {
            t.HasCheckConstraint("ck_treinamento_duracao", "duracao_minutos BETWEEN 1 AND 600000");
            t.HasCheckConstraint("ck_treinamento_renovacao", "renovar_em IS NULL OR renovar_em >= concluido_em");
            t.HasCheckConstraint("ck_treinamento_comprovante", "(comprovante_documento_id IS NULL AND comprovante_versao_id IS NULL AND comprovante_numero_versao IS NULL AND comprovante_revisao_documento IS NULL AND comprovante_hash IS NULL AND comprovante_revisao_treinamento IS NULL AND comprovante_vinculado_em IS NULL) OR (comprovante_documento_id IS NOT NULL AND comprovante_versao_id IS NOT NULL AND comprovante_numero_versao IS NOT NULL AND comprovante_revisao_documento IS NOT NULL AND comprovante_hash IS NOT NULL AND comprovante_revisao_treinamento IS NOT NULL AND comprovante_numero_versao > 0 AND comprovante_revisao_documento > 0 AND comprovante_hash ~ '^[a-f0-9]{64}$' AND comprovante_revisao_treinamento > 0 AND comprovante_revisao_treinamento <= revisao AND comprovante_vinculado_em IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CompanyId).HasColumnName("empresa_id"); b.Property(x => x.UnitId).HasColumnName("unidade_id");
        b.Property(x => x.WorkerId).HasColumnName("trabalhador_id"); b.Property(x => x.CommandId).HasColumnName("comando_id");
        b.Property(x => x.Title).HasColumnName("titulo").HasMaxLength(160).IsRequired();
        b.Property(x => x.Reference).HasColumnName("referencia").HasMaxLength(40);
        b.Property(x => x.Kind).HasColumnName("tipo").HasMaxLength(15).IsRequired();
        b.Property(x => x.CompletedOn).HasColumnName("concluido_em"); b.Property(x => x.RenewalOn).HasColumnName("renovar_em");
        b.Property(x => x.DurationMinutes).HasColumnName("duracao_minutos");
        b.Property(x => x.Provider).HasColumnName("instituicao").HasMaxLength(160);
        b.Property(x => x.Instructor).HasColumnName("instrutor").HasMaxLength(160);
        b.Property(x => x.Venue).HasColumnName("local").HasMaxLength(160);
        b.Property(x => x.Content).HasColumnName("conteudo").HasMaxLength(2000);
        b.Property(x => x.Notes).HasColumnName("observacoes").HasMaxLength(500);
        b.Property(x => x.RevokedAt).HasColumnName("invalidado_em");
        b.Property(x => x.RevocationReason).HasColumnName("motivo_invalidacao").HasMaxLength(200);
        b.Property(x => x.Revision).HasColumnName("revisao").IsConcurrencyToken();
        b.Property(x => x.EvidenceDocumentId).HasColumnName("comprovante_documento_id");
        b.Property(x => x.EvidenceVersionId).HasColumnName("comprovante_versao_id");
        b.Property(x => x.EvidenceVersionNumber).HasColumnName("comprovante_numero_versao");
        b.Property(x => x.EvidenceDocumentRevision).HasColumnName("comprovante_revisao_documento");
        b.Property(x => x.EvidenceFileHash).HasColumnName("comprovante_hash").HasMaxLength(64);
        b.Property(x => x.EvidenceTrainingRevision).HasColumnName("comprovante_revisao_treinamento");
        b.Property(x => x.EvidenceLinkedAt).HasColumnName("comprovante_vinculado_em");
        b.Property(x => x.Active).HasColumnName("ativo"); b.Property(x => x.CreatedAt).HasColumnName("criado_em");
        b.Property(x => x.CreatedBy).HasColumnName("criado_por"); b.Property(x => x.UpdatedAt).HasColumnName("atualizado_em");
        b.Property(x => x.UpdatedBy).HasColumnName("atualizado_por"); b.Property(x => x.DeletedAt).HasColumnName("excluido_em");
        b.Property(x => x.DeletedBy).HasColumnName("excluido_por");
        b.HasIndex(x => new { x.CompanyId, x.CommandId }).IsUnique();
        b.HasIndex(x => new { x.UnitId, x.WorkerId, x.RenewalOn });
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Worker>().WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Document>().WithMany().HasForeignKey(x => x.EvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DocumentVersion>().WithMany().HasForeignKey(x => x.EvidenceVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
