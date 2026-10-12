using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Audit.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Audit;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("log_auditoria", schema: "audit");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.ActorId).HasColumnName("ator_id");
        builder.Property(a => a.ActorEmail).HasColumnName("ator_email").HasMaxLength(200);
        builder.Property(a => a.ActorName).HasColumnName("ator_nome").HasMaxLength(200);

        builder.Property(a => a.Action)
            .HasColumnName("acao")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.ResourceType)
            .HasColumnName("recurso_tipo")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(a => a.ResourceId).HasColumnName("recurso_id");

        builder.Property(a => a.PreviousData).HasColumnName("dados_anteriores").HasColumnType("jsonb");
        builder.Property(a => a.NewData).HasColumnName("dados_novos").HasColumnType("jsonb");
        builder.Property(a => a.Context).HasColumnName("contexto").HasColumnType("jsonb");

        builder.Property(a => a.IpAddress).HasColumnName("ip").HasColumnType("inet");
        builder.Property(a => a.UserAgent).HasColumnName("user_agent").HasColumnType("text");

        builder.Property(a => a.PreviousHash).HasColumnName("hash_anterior").HasColumnType("char(64)");

        builder.Property(a => a.CurrentHash)
            .HasColumnName("hash_atual")
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(a => a.OccurredAt)
            .HasColumnName("ocorrido_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(a => a.OccurredAt).HasDatabaseName("ix_auditoria_ocorrido_em");
        builder.HasIndex(a => new { a.ActorId, a.OccurredAt }).HasDatabaseName("ix_auditoria_ator");
        builder.HasIndex(a => new { a.ResourceType, a.ResourceId }).HasDatabaseName("ix_auditoria_recurso");
        builder.HasIndex(a => a.CurrentHash).HasDatabaseName("ix_auditoria_hash_atual");
    }
}