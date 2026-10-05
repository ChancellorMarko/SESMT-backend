using SESMT.Domain.Common.Entity;

namespace SESMT.Domain.Modules.Organization.Entities;

public sealed class Sector : EntityBase
{
    public Guid UnitId { get; private set; }

    public string Name { get; private set; } = null!;

    private Sector()
    {
    }

    public Sector(Guid unitId, string name)
    {
        UnitId = unitId;
        Name = name;
    }
}