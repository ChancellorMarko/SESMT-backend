using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Organization.Entities;

public sealed class Company : AggregateRootBase
{
    public string Name { get; private set; } = null!;

    private Company()
    {
    }

    public Company(string name)
    {
        Name = name;
    }
}