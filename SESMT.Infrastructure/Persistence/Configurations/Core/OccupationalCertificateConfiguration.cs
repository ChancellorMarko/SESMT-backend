using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.OccupationalHealth.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class OccupationalCertificateConfiguration : IEntityTypeConfiguration<OccupationalCertificate>
{
    public void Configure(EntityTypeBuilder<OccupationalCertificate> b)
    {
        b.ToTable("aso_registro", "saude", t => {
            t.HasCheckConstraint("ck_aso_datas", "emitido_em >= exame_em AND (proxima_avaliacao_em IS NULL OR proxima_avaliacao_em >= exame_em)");
            t.HasCheckConstraint("ck_aso_tipo", "tipo_exame IN ('ADMISSION','PERIODIC','RETURN_TO_WORK','RISK_CHANGE','DISMISSAL')");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CompanyId).HasColumnName("empresa_id"); b.Property(x => x.UnitId).HasColumnName("unidade_id");
        b.Property(x => x.WorkerId).HasColumnName("trabalhador_id"); b.Property(x => x.CommandId).HasColumnName("comando_id");
        b.Property(x => x.ExamKind).HasColumnName("tipo_exame").HasMaxLength(20).IsRequired();
        b.Property(x => x.ExamOn).HasColumnName("exame_em"); b.Property(x => x.IssuedOn).HasColumnName("emitido_em");
        b.Property(x => x.NextEvaluationOn).HasColumnName("proxima_avaliacao_em");
        b.Property(x => x.ProtectedDetails).HasColumnName("dados_protegidos").HasColumnType("text").IsRequired();
        b.Property(x => x.RevokedAt).HasColumnName("invalidado_em"); b.Property(x => x.RevocationCode).HasColumnName("motivo_invalidacao").HasMaxLength(20);
        b.Property(x => x.Revision).HasColumnName("revisao").IsConcurrencyToken();
        b.Property(x => x.Active).HasColumnName("ativo"); b.Property(x => x.CreatedAt).HasColumnName("criado_em");
        b.Property(x => x.CreatedBy).HasColumnName("criado_por"); b.Property(x => x.UpdatedAt).HasColumnName("atualizado_em");
        b.Property(x => x.UpdatedBy).HasColumnName("atualizado_por"); b.Property(x => x.DeletedAt).HasColumnName("excluido_em");
        b.Property(x => x.DeletedBy).HasColumnName("excluido_por");
        b.HasIndex(x => new { x.CompanyId, x.CommandId }).IsUnique(); b.HasIndex(x => new { x.UnitId, x.WorkerId, x.ExamOn });
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Worker>().WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
    }
}
