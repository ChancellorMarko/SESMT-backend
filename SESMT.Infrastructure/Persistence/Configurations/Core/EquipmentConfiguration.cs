using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Common.Entity;
using SESMT.Domain.Modules.Equipment.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<ProtectiveEquipment>
{
    public void Configure(EntityTypeBuilder<ProtectiveEquipment> b)
    {
        b.ToTable("epi", "core"); EquipmentMapping.Common(b);
        b.Property(x => x.CompanyId).HasColumnName("empresa_id");
        b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasColumnName("nome").HasMaxLength(160).IsRequired();
        b.Property(x => x.ApprovalCertificate).HasColumnName("ca").HasMaxLength(30);
        b.Property(x => x.Manufacturer).HasColumnName("fabricante").HasMaxLength(120);
        b.Property(x => x.Model).HasColumnName("modelo").HasMaxLength(120);
        b.Property(x => x.Revision).HasColumnName("revisao").IsConcurrencyToken();
        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_epi_empresa_codigo");
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class EquipmentDeliveryConfiguration : IEntityTypeConfiguration<EquipmentDelivery>
{
    public void Configure(EntityTypeBuilder<EquipmentDelivery> b)
    {
        b.ToTable("epi_entrega", "core", t => t.HasCheckConstraint("ck_epi_entrega_quantidade", "quantidade BETWEEN 1 AND 1000")); EquipmentMapping.Common(b);
        b.Property(x => x.CompanyId).HasColumnName("empresa_id"); b.Property(x => x.UnitId).HasColumnName("unidade_id");
        b.Property(x => x.WorkerId).HasColumnName("trabalhador_id"); b.Property(x => x.EquipmentId).HasColumnName("epi_id");
        b.Property(x => x.CommandId).HasColumnName("comando_id");
        b.Property(x => x.EquipmentName).HasColumnName("epi_nome").HasMaxLength(160).IsRequired();
        b.Property(x => x.EquipmentCode).HasColumnName("epi_codigo").HasMaxLength(30).IsRequired();
        b.Property(x => x.ApprovalCertificate).HasColumnName("ca").HasMaxLength(30);
        b.Property(x => x.Quantity).HasColumnName("quantidade"); b.Property(x => x.Size).HasColumnName("tamanho").HasMaxLength(30);
        b.Property(x => x.DeliveredOn).HasColumnName("entregue_em"); b.Property(x => x.ReplacementOn).HasColumnName("troca_prevista_em");
        b.Property(x => x.ReturnedOn).HasColumnName("devolvido_em"); b.Property(x => x.ReturnReason).HasColumnName("motivo_devolucao").HasMaxLength(200);
        b.Property(x => x.Notes).HasColumnName("observacoes").HasMaxLength(500);
        b.Property(x => x.Revision).HasColumnName("revisao").IsConcurrencyToken();
        b.HasIndex(x => new { x.CompanyId, x.CommandId }).IsUnique(); b.HasIndex(x => new { x.UnitId, x.WorkerId, x.DeliveredOn });
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Worker>().WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProtectiveEquipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal static class EquipmentMapping
{
    public static void Common<T>(EntityTypeBuilder<T> b) where T : EntityBase
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Active).HasColumnName("ativo"); b.Property(x => x.CreatedAt).HasColumnName("criado_em");
        b.Property(x => x.CreatedBy).HasColumnName("criado_por"); b.Property(x => x.UpdatedAt).HasColumnName("atualizado_em");
        b.Property(x => x.UpdatedBy).HasColumnName("atualizado_por"); b.Property(x => x.DeletedAt).HasColumnName("excluido_em");
        b.Property(x => x.DeletedBy).HasColumnName("excluido_por");
    }
}
