using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class AppointmentInviteConfiguration : IEntityTypeConfiguration<AppointmentInvite>
{
    public void Configure(EntityTypeBuilder<AppointmentInvite> b)
    {
        b.ToTable("convite_agendamento", "core"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CompanyId).HasColumnName("empresa_id");
        b.Property(x => x.TeamId).HasColumnName("equipe_id");
        b.Property(x => x.LocationId).HasColumnName("local_id");
        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.ExpiresAt).HasColumnName("expira_em").HasColumnType("timestamptz");
        b.Property(x => x.RevokedAt).HasColumnName("revogado_em").HasColumnType("timestamptz");
        b.Property(x => x.RevokedBy).HasColumnName("revogado_por");
        b.Property(x => x.Version).HasColumnName("versao").IsConcurrencyToken();
        b.Property(x => x.Active).HasColumnName("ativo").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz");
        b.Property(x => x.CreatedBy).HasColumnName("criado_por");
        b.Property(x => x.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        b.Property(x => x.UpdatedBy).HasColumnName("atualizado_por");
        b.Property(x => x.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        b.Property(x => x.DeletedBy).HasColumnName("excluido_por");
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new {x.CompanyId, x.TeamId, x.ExpiresAt});
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
