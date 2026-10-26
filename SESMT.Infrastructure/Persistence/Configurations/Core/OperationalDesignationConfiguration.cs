using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class OperationalDesignationConfiguration : IEntityTypeConfiguration<OperationalDesignation>
{
    public void Configure(EntityTypeBuilder<OperationalDesignation> builder)
    {
        builder.ToTable("designacao_operacional", "core");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CompanyId).HasColumnName("empresa_id").IsRequired();
        builder.Property(x => x.PersonId).HasColumnName("pessoa_id").IsRequired();
        builder.Property(x => x.ServiceIds).HasColumnName("servico_ids").HasColumnType("uuid[]").IsRequired();
        builder.Property(x => x.LocationIds).HasColumnName("local_ids").HasColumnType("uuid[]").IsRequired();
        builder.Property(x => x.Type).HasColumnName("tipo").HasMaxLength(16).IsRequired();
        builder.Property(x => x.StartsAt).HasColumnName("inicio_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.EndsAt).HasColumnName("fim_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Origin).HasColumnName("origem").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AssignedBy).HasColumnName("atribuido_por").IsRequired();
        builder.Property(x => x.RevokedBy).HasColumnName("revogado_por");
        builder.Property(x => x.RevokedAt).HasColumnName("revogado_em").HasColumnType("timestamptz");
        builder.Property(x => x.Version).HasColumnName("versao").IsConcurrencyToken().IsRequired();
        builder.Property(x => x.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("criado_por");
        builder.Property(x => x.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(x => x.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(x => x.DeletedBy).HasColumnName("excluido_por");
        builder.HasOne<SESMT.Domain.Modules.Organization.Entities.Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.Workforce.Entities.Worker>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.IdentityAccess.Entities.User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.IdentityAccess.Entities.User>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CompanyId, x.PersonId, x.Type }).HasDatabaseName("ix_designacao_empresa_pessoa_tipo");
        builder.HasIndex(x => new { x.Active, x.StartsAt, x.EndsAt }).HasDatabaseName("ix_designacao_vigencia");
        builder.HasIndex(x => new { x.CompanyId, x.PersonId, x.Type, x.ServiceIds, x.LocationIds, x.StartsAt, x.EndsAt, x.Origin })
            .IsUnique().HasDatabaseName("ux_designacao_comando_idempotente");
    }
}
