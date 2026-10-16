using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class WorkPermitDocumentRequirementConfiguration : IEntityTypeConfiguration<WorkPermitDocumentRequirement>
{
    public void Configure(EntityTypeBuilder<WorkPermitDocumentRequirement> builder)
    {
        builder.ToTable("permissao_trabalho_requisito_documental", "core");
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(requirement => requirement.CompanyId).HasColumnName("empresa_id").IsRequired();
        builder.Property(requirement => requirement.ServiceCode).HasColumnName("servico_codigo").HasMaxLength(80).IsRequired();
        builder.Property(requirement => requirement.DocumentTypeId).HasColumnName("tipo_documento_id").IsRequired();
        builder.Property(requirement => requirement.Description).HasColumnName("descricao").HasMaxLength(300).IsRequired();
        builder.Property(requirement => requirement.ConfiguredBy).HasColumnName("configurado_por").IsRequired();
        builder.Property(requirement => requirement.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(requirement => requirement.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(requirement => requirement.CreatedBy).HasColumnName("criado_por");
        builder.Property(requirement => requirement.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(requirement => requirement.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(requirement => requirement.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(requirement => requirement.DeletedBy).HasColumnName("excluido_por");
        builder.HasOne<SESMT.Domain.Modules.Organization.Entities.Company>().WithMany()
            .HasForeignKey(requirement => requirement.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.Documents.Entities.DocumentType>().WithMany()
            .HasForeignKey(requirement => requirement.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SESMT.Domain.Modules.IdentityAccess.Entities.User>().WithMany()
            .HasForeignKey(requirement => requirement.ConfiguredBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(requirement => new { requirement.CompanyId, requirement.ServiceCode, requirement.DocumentTypeId })
            .IsUnique().HasFilter("excluido_em IS NULL").HasDatabaseName("ux_pt_requisito_servico_tipo_doc");
    }
}
