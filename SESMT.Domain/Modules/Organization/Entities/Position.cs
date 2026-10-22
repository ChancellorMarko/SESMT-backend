using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Organization.Entities;

public class Position : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? CBO { get; private set; }
    public string? Description { get; private set; }

    protected Position() { }

    public Position(Guid companyId, string name, string? cbo, string? description)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("ID da empresa é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do cargo é obrigatório.");
        }

        CompanyId = companyId;
        Name = name.Trim();
        CBO = cbo?.Trim();
        Description = description?.Trim();
    }

    public void Update(string name, string? cbo, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do cargo é obrigatório.");
        }

        Name = name.Trim();
        CBO = cbo?.Trim();
        Description = description?.Trim();
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        MarkAsDeleted();
    }
}