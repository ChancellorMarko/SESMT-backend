using SESMT.Domain.Modules.Organization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class WorkerHistoryConfiguration : IEntityTypeConfiguration<WorkerHistory>
{
    public void Configure(EntityTypeBuilder<WorkerHistory> builder)
    {
        // Integrity relations without navigation: EF orders inserts and the database rejects orphans.
        builder.HasOne<Worker>().WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("trabalhador_historico", schema: "core");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(h => h.WorkerId).HasColumnName("trabalhador_id").IsRequired();
        builder.Property(h => h.DepartmentId).HasColumnName("setor_id").IsRequired();
        builder.Property(h => h.PositionId).HasColumnName("cargo_id").IsRequired();

        builder.Property(h => h.StartDate).HasColumnName("data_inicio").HasColumnType("date").IsRequired();
        builder.Property(h => h.EndDate).HasColumnName("data_fim").HasColumnType("date");

        builder.Property(h => h.ReasonForChange)
            .HasColumnName("motivo")
            .HasMaxLength(200);

        MapAuditFields(builder);

        builder.HasIndex(h => new { h.WorkerId, h.StartDate })
            .HasDatabaseName("ix_historico_trabalhador");
    }

    private static void MapAuditFields(EntityTypeBuilder<WorkerHistory> b)
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