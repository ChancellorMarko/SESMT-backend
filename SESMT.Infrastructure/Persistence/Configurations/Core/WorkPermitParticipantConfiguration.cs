using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class WorkPermitParticipantConfiguration : IEntityTypeConfiguration<WorkPermitParticipant>
{
    public void Configure(EntityTypeBuilder<WorkPermitParticipant> builder)
    {
        builder.ToTable("permissao_trabalho_participante", "core");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(participant => participant.WorkPermitId).HasColumnName("permissao_trabalho_id").IsRequired();
        builder.Property(participant => participant.UserId).HasColumnName("usuario_id").IsRequired();
        builder.Property(participant => participant.Responsibility).HasColumnName("responsabilidade").HasMaxLength(80).IsRequired();
        builder.Property(participant => participant.AssignedBy).HasColumnName("atribuido_por").IsRequired();
        builder.Property(participant => participant.AcknowledgedAt).HasColumnName("ciente_em").HasColumnType("timestamptz");
        builder.Property(participant => participant.AcknowledgedVersion).HasColumnName("ciente_versao");
        builder.Property(participant => participant.AcknowledgementsJson).HasColumnName("ciencias_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Ignore(participant => participant.Acknowledgements);
        builder.Property(participant => participant.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(participant => participant.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(participant => participant.CreatedBy).HasColumnName("criado_por");
        builder.Property(participant => participant.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(participant => participant.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(participant => participant.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(participant => participant.DeletedBy).HasColumnName("excluido_por");
        builder.HasOne<SESMT.Domain.Modules.IdentityAccess.Entities.User>().WithMany()
            .HasForeignKey(participant => participant.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.IdentityAccess.Entities.User>().WithMany()
            .HasForeignKey(participant => participant.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(participant => new { participant.WorkPermitId, participant.UserId })
            .IsUnique().HasFilter("excluido_em IS NULL").HasDatabaseName("ux_permissao_trabalho_participante");
    }
}
