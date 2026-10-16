using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed class WorkPermitDocumentRequirement : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string ServiceCode { get; private set; } = null!;
    public Guid DocumentTypeId { get; private set; }
    public string Description { get; private set; } = null!;
    public Guid ConfiguredBy { get; private set; }

    private WorkPermitDocumentRequirement() { }

    public WorkPermitDocumentRequirement(Guid companyId, string serviceCode, Guid documentTypeId, string description, Guid configuredBy)
    {
        if (companyId == Guid.Empty || documentTypeId == Guid.Empty || configuredBy == Guid.Empty)
            throw new ArgumentException("Empresa, tipo documental e responsável pela configuração são obrigatórios.");
        if (string.IsNullOrWhiteSpace(serviceCode) || string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Serviço e descrição do requisito são obrigatórios.");

        CompanyId = companyId;
        ServiceCode = serviceCode.Trim().ToUpperInvariant();
        DocumentTypeId = documentTypeId;
        Description = description.Trim();
        ConfiguredBy = configuredBy;
        CreatedBy = configuredBy;
    }
}
