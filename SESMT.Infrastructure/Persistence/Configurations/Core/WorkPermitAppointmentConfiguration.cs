using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class WorkPermitAppointmentConfiguration : IEntityTypeConfiguration<WorkPermitAppointment>
{
    public void Configure(EntityTypeBuilder<WorkPermitAppointment> builder)
    {
        builder.ToTable("agendamento_pt", "core");
        builder.HasKey(appointment => appointment.Id);
        builder.Property(appointment => appointment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(appointment => appointment.CompanyId).HasColumnName("empresa_id").IsRequired();
        builder.Property(appointment => appointment.LocationId).HasColumnName("local_id");
        builder.Property(appointment => appointment.Title).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(appointment => appointment.StartsAt).HasColumnName("inicio_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(appointment => appointment.EndsAt).HasColumnName("fim_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(appointment => appointment.RequestedBy).HasColumnName("solicitado_por").IsRequired();
        MapAuditFields(builder);
        builder.HasOne<SESMT.Domain.Modules.Organization.Entities.Company>().WithMany()
            .HasForeignKey(appointment => appointment.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.IdentityAccess.Entities.User>().WithMany()
            .HasForeignKey(appointment => appointment.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.Organization.Entities.Unit>().WithMany()
            .HasForeignKey(appointment => appointment.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(appointment => new { appointment.CompanyId, appointment.StartsAt })
            .HasFilter("excluido_em IS NULL").HasDatabaseName("ix_agendamento_pt_empresa_inicio");
    }

    private static void MapAuditFields(EntityTypeBuilder<WorkPermitAppointment> builder)
    {
        builder.Property(x => x.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("criado_por");
        builder.Property(x => x.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(x => x.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(x => x.DeletedBy).HasColumnName("excluido_por");
    }
}
