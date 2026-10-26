using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Notifications.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerta", schema: "core");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(a => a.Type)
            .HasColumnName("tipo")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnName("entidade_tipo")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.EntityId).HasColumnName("entidade_id").IsRequired();
        builder.Property(a => a.SourceEventId).HasColumnName("evento_origem_id").IsRequired();

        builder.Property(a => a.Severity)
            .HasColumnName("severidade")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.DestinationUserId).HasColumnName("destinatario_id").IsRequired();

        builder.Property(a => a.Channel)
            .HasColumnName("canal")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("titulo")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Message).HasColumnName("mensagem").HasColumnType("text");

        builder.Property(a => a.SentAt)
            .HasColumnName("data_disparo")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.ReadAt)
            .HasColumnName("data_leitura")
            .HasColumnType("timestamptz");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.SeverityLevel)
            .HasColumnName("escalonamento_nivel")
            .HasDefaultValue(0)
            .IsRequired();

        MapAuditFields(builder);

        builder.HasIndex(a => new { a.DestinationUserId, a.Status, a.SentAt })
            .HasDatabaseName("ix_alerta_destinatario_status");
        builder.HasIndex(a => new { a.SourceEventId, a.DestinationUserId })
            .IsUnique()
            .HasDatabaseName("ux_alerta_evento_destinatario");
    }

    private static void MapAuditFields(EntityTypeBuilder<Alert> b)
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
