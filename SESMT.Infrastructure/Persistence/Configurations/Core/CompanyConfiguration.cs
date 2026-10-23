using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("empresa", schema: "core");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        // ─── Cnpj Value Object ──────────────────────────────────────────────
        builder.OwnsOne(c => c.Cnpj, cnpj =>
        {
            cnpj.Property(v => v.Value)
                .HasColumnName("cnpj")
                .HasMaxLength(14)
                .IsRequired();

            cnpj.HasIndex(v => v.Value)
                .IsUnique()
                .HasFilter("excluido_em IS NULL")
                .HasDatabaseName("ux_empresa_cnpj");
        });

        builder.Property(c => c.CorporateName)
            .HasColumnName("razao_social")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.FantasyName)
            .HasColumnName("nome_fantasia")
            .HasMaxLength(255)
            .IsRequired();

        // ─── Email Value Object (nullable) ──────────────────────────────────
        builder.OwnsOne(c => c.Email, email =>
        {
            email.Property(v => v.Value)
                .HasColumnName("email")
                .HasMaxLength(200);
        });

        builder.Property(c => c.Phone)
            .HasColumnName("telefone")
            .HasMaxLength(20);

        // ─── Auditoria ──────────────────────────────────────────────────────
        MapAuditFields(builder);
    }

    private static void MapAuditFields(EntityTypeBuilder<Company> b)
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
