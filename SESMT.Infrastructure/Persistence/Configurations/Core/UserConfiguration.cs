using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.IdentityAccess.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("usuario", schema: "core");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.Name)
            .HasColumnName("nome")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("senha_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.MfaEnabled)
            .HasColumnName("mfa_habilitado")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.MfaSecretEncrypted)
            .HasColumnName("mfa_secret_enc")
            .HasColumnType("bytea");

        builder.Property(u => u.LastLogin)
            .HasColumnName("ultimo_login")
            .HasColumnType("timestamptz");

        builder.Property(u => u.BlockedUntil)
            .HasColumnName("bloqueado_ate")
            .HasColumnType("timestamptz");

        builder.Property(u => u.LoginAttempts)
            .HasColumnName("tentativas_login")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(u => u.Active)
            .HasColumnName("ativo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz");

        builder.Property(u => u.DeletedAt)
            .HasColumnName("excluido_em")
            .HasColumnType("timestamptz");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ux_usuario_email");

        builder.HasIndex(u => u.Active)
            .HasDatabaseName("ix_usuario_ativo");
    }
}