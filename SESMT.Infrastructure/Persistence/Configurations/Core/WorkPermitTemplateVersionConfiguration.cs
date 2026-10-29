using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SESMT.Domain.Modules.WorkPermits.Entities;

namespace SESMT.Infrastructure.Persistence.Configurations.Core;

public sealed class WorkPermitTemplateVersionConfiguration : IEntityTypeConfiguration<WorkPermitTemplateVersion>
{
    public void Configure(EntityTypeBuilder<WorkPermitTemplateVersion> builder)
    {
        builder.ToTable("pt_template_versao", "core");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(template => template.CompanyId).HasColumnName("empresa_id").IsRequired();
        builder.Property(template => template.TemplateId).HasColumnName("template_id").IsRequired();
        builder.Property(template => template.Name).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(template => template.Version).HasColumnName("versao").IsRequired();
        builder.Property(template => template.ServiceCodesJson).HasColumnName("servicos_json").HasColumnType("jsonb").IsRequired();
        builder.Property(template => template.QuestionsJson).HasColumnName("perguntas_json").HasColumnType("jsonb").IsRequired();
        builder.Property(template => template.ClosingQuestionsJson).HasColumnName("encerramento_perguntas_json").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(template => template.Declaration).HasColumnName("declaracao").HasMaxLength(2000).IsRequired();
        builder.Property(template => template.Illustrative).HasColumnName("ilustrativo").IsRequired();
        builder.Ignore(template => template.ServiceCodes);
        builder.Ignore(template => template.Questions);
        builder.Ignore(template => template.ClosingQuestions);
        builder.Property(template => template.Active).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(template => template.CreatedAt).HasColumnName("criado_em").HasColumnType("timestamptz").IsRequired();
        builder.Property(template => template.CreatedBy).HasColumnName("criado_por");
        builder.Property(template => template.UpdatedAt).HasColumnName("atualizado_em").HasColumnType("timestamptz");
        builder.Property(template => template.UpdatedBy).HasColumnName("atualizado_por");
        builder.Property(template => template.DeletedAt).HasColumnName("excluido_em").HasColumnType("timestamptz");
        builder.Property(template => template.DeletedBy).HasColumnName("excluido_por");
        builder.HasOne<SESMT.Domain.Modules.Organization.Entities.Company>().WithMany()
            .HasForeignKey(template => template.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(template => new { template.TemplateId, template.Version }).IsUnique()
            .HasDatabaseName("ux_pt_template_versao");
        builder.HasIndex(template => new { template.CompanyId, template.TemplateId });
    }
}
