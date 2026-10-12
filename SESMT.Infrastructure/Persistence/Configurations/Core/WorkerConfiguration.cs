using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.Workforce.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        builder.ToTable("trabalhador", schema: "core");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(w => w.CompanyId).HasColumnName("empresa_id").IsRequired();

        builder.Property(w => w.Enrollment)
            .HasColumnName("matricula")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(w => w.CpfEncryption)
            .HasColumnName("cpf_enc")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(w => w.CpfHash)
            .HasColumnName("cpf_hash")
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(w => w.Name)
            .HasColumnName("nome")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(w => w.DateOfBirth).HasColumnName("data_nascimento").HasColumnType("date");
        builder.Property(w => w.AdmissionDate).HasColumnName("data_admissao").HasColumnType("date");
        builder.Property(w => w.DismissalDate).HasColumnName("data_demissao").HasColumnType("date");

        builder.Property(w => w.PositionId).HasColumnName("cargo_id");
        builder.Property(w => w.UnitId).HasColumnName("unidade_id");
        builder.Property(w => w.UserId).HasColumnName("usuario_id");

        MapAuditFields(builder);

        builder.HasIndex(w => new { w.CompanyId, w.Enrollment })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_trabalhador_empresa_matricula");

        builder.HasIndex(w => new { w.CompanyId, w.CpfHash })
            .IsUnique()
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ux_trabalhador_empresa_cpf_hash");

        builder.HasIndex(w => w.Active).HasDatabaseName("ix_trabalhador_ativo");
    }

    private static void MapAuditFields(EntityTypeBuilder<Worker> b)
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