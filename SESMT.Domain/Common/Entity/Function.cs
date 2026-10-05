using SESMT.Domain.Common.Entity;

namespace SESMT.Domain.Modules.Organization.Entities;

public sealed class Function : EntityBase
{
    public Guid SectorId { get; private set; }

    public string Name { get; private set; } = null!;

    private Function()
    {
    }

    public Function(Guid sectorId, string name)
    {
        SectorId = sectorId;
        Name = name;
    }
}