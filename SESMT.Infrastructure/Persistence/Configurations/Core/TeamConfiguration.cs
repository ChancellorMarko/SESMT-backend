using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("equipe", schema: "core");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.OrganizationId).HasColumnName("organizacao_id").IsRequired();
        builder.Property(t => t.CompanyId).HasColumnName("empresa_id");
        builder.Property(t => t.LocalId).HasColumnName("local_id");
        builder.Property(t => t.ChiefId).HasColumnName("chefe_id");

        builder.Property(t => t.Name)
            .HasColumnName("nome")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("descricao")
            .HasColumnType("text");

        builder.Property(t => t.ParticipantIds)
            .HasColumnName("participantes_ids")
            .HasColumnType("uuid[]");

        builder.Property(t => t.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("criado_por");
        builder.Property(t => t.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(t => t.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(t => t.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(t => t.DeletedBy).HasColumnName("excluido_por");

        builder.HasIndex(t => t.OrganizationId)
            .HasDatabaseName("ix_equipe_organizacao");

        builder.HasIndex(t => t.CompanyId)
            .HasDatabaseName("ix_equipe_empresa");

        builder.HasIndex(t => new { t.OrganizationId, t.Name })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_equipe_organizacao_nome");

        builder.HasIndex(t => t.ParticipantIds)
            .HasMethod("GIN")
            .HasDatabaseName("ix_equipe_participantes");
    }
}