using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("usuario_token_redefinicao_senha", "core");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.UserId).HasColumnName("usuario_id").IsRequired();
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expira_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(token => token.UsedAt).HasColumnName("utilizado_em").HasColumnType("timestamptz");
        builder.Property(token => token.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(token => token.CreatedBy).HasColumnName("criado_por");
        builder.Property(token => token.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(token => token.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(token => token.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(token => token.DeletedBy).HasColumnName("excluido_por");
        builder.Property(token => token.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_usuario_token_redefinicao_hash");
        builder.HasIndex(token => token.UserId).HasDatabaseName("ix_usuario_token_redefinicao_usuario");
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
