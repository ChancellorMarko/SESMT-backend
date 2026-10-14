using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class UserSessionConfiguration
    : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("usuario_sessao", schema: "core");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(session => session.UserId)
            .HasColumnName("usuario_id")
            .IsRequired();

        builder.Property(session => session.RefreshTokenHash)
            .HasColumnName("refresh_token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(session => session.ExpiresAt)
            .HasColumnName("expira_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(session => session.RevokedAt)
            .HasColumnName("revogado_em")
            .HasColumnType("timestamptz");

        // Auditoria herdada de EntityBase
        builder.Property(session => session.CreatedAt)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(session => session.CreatedBy)
            .HasColumnName("criado_por");

        builder.Property(session => session.UpdatedAt)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz");

        builder.Property(session => session.UpdatedBy)
            .HasColumnName("atualizado_por");

        builder.Property(session => session.DeletedAt)
            .HasColumnName("excluido_em")
            .HasColumnType("timestamptz");

        builder.Property(session => session.DeletedBy)
            .HasColumnName("excluido_por");

        builder.Property(session => session.Active)
            .HasColumnName("ativo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(session => session.UserId)
            .HasDatabaseName("ix_usuario_sessao_usuario");

        builder.HasIndex(session => session.RefreshTokenHash)
            .IsUnique()
            .HasDatabaseName("ux_usuario_sessao_refresh_hash");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}