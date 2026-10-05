using SESMT.Domain.Common.Entity;

namespace SESMT.Domain.Modules.Organization.Entities;

public sealed class Unit : EntityBase
{
    public Guid CompanyId { get; private set; }

    public string Name { get; private set; } = null!;

    private Unit()
    {
    }

    public Unit(Guid companyId, string name)
    {
        CompanyId = companyId;
        Name = name;
    }
}