using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class WorkPermitConfiguration : IEntityTypeConfiguration<WorkPermit>
{
    public void Configure(EntityTypeBuilder<WorkPermit> builder)
    {
        builder.ToTable("permissao_trabalho", schema: "core");

        builder.HasKey(permit => permit.Id);
        builder.Property(permit => permit.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(permit => permit.CompanyId).HasColumnName("empresa_id").IsRequired();
        builder.Property(permit => permit.AppointmentId).HasColumnName("agendamento_id").IsRequired(false);
        builder.Property(permit => permit.ServiceCode).HasColumnName("servico_codigo").HasMaxLength(80).IsRequired();
        builder.Property(permit => permit.ContentVersion).HasColumnName("conteudo_versao").HasDefaultValue(1).IsRequired();
        builder.Property(permit => permit.Title).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(permit => permit.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(permit => permit.Version).HasColumnName("versao").IsConcurrencyToken().IsRequired();
        builder.Property(permit => permit.ReleasedBy).HasColumnName("liberada_por");
        builder.Property(permit => permit.ReleasedAt).HasColumnName("liberada_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.StartedBy).HasColumnName("iniciada_por");
        builder.Property(permit => permit.StartedAt).HasColumnName("iniciada_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.SuspendedBy).HasColumnName("suspensa_por");
        builder.Property(permit => permit.SuspendedAt).HasColumnName("suspensa_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.SuspensionReason).HasColumnName("motivo_suspensao").HasMaxLength(1000);
        builder.Property(permit => permit.CompletedBy).HasColumnName("concluida_por");
        builder.Property(permit => permit.CompletedAt).HasColumnName("concluida_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.CancelledBy).HasColumnName("cancelada_por");
        builder.Property(permit => permit.CancelledAt).HasColumnName("cancelada_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.CancellationReason).HasColumnName("motivo_cancelamento").HasMaxLength(1000);
        builder.Property(permit => permit.TemplateVersionIdsJson).HasColumnName("template_versoes_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(permit => permit.ChecklistAnswersJson).HasColumnName("checklist_respostas_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Ignore(permit => permit.TemplateVersionIds);
        builder.Ignore(permit => permit.ChecklistAnswers);

        builder.Property(permit => permit.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(permit => permit.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(permit => permit.CreatedBy).HasColumnName("criado_por");
        builder.Property(permit => permit.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(permit => permit.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(permit => permit.DeletedBy).HasColumnName("excluido_por");

        builder.HasOne<SESMT.Domain.Modules.Organization.Entities.Company>()
            .WithMany()
            .HasForeignKey(permit => permit.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkPermitAppointment>()
            .WithMany()
            .HasForeignKey(permit => permit.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(permit => permit.AppointmentId)
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_permissao_trabalho_agendamento");

        builder.HasIndex(permit => new { permit.CompanyId, permit.Status })
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_permissao_trabalho_empresa_status");

        builder.HasMany(permit => permit.Transitions)
            .WithOne()
            .HasForeignKey(transition => transition.WorkPermitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(permit => permit.Participants)
            .WithOne()
            .HasForeignKey(participant => participant.WorkPermitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(permit => permit.Transitions)
            .HasField("_transitions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(permit => permit.Participants)
            .HasField("_participants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
