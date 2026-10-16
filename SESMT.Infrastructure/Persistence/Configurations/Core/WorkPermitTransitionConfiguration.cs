using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class WorkPermitTransitionConfiguration : IEntityTypeConfiguration<WorkPermitTransition>
{
    public void Configure(EntityTypeBuilder<WorkPermitTransition> builder)
    {
        builder.ToTable("permissao_trabalho_historico", schema: "core");
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(transition => transition.WorkPermitId).HasColumnName("permissao_trabalho_id").IsRequired();
        builder.Property(transition => transition.PreviousStatus).HasColumnName("status_anterior").HasMaxLength(30);
        builder.Property(transition => transition.NewStatus).HasColumnName("status_novo").HasMaxLength(30).IsRequired();
        builder.Property(transition => transition.Action).HasColumnName("acao").HasMaxLength(30).IsRequired();
        builder.Property(transition => transition.ActorId).HasColumnName("ator_id").IsRequired();
        builder.Property(transition => transition.OccurredAt).HasColumnName("ocorrido_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(transition => transition.Reason).HasColumnName("motivo").HasMaxLength(1000);
        builder.Property(transition => transition.Version).HasColumnName("versao").IsRequired();
        builder.HasIndex(transition => new { transition.WorkPermitId, transition.Version })
            .IsUnique()
            .HasDatabaseName("ux_permissao_trabalho_historico_versao");
    }
}
