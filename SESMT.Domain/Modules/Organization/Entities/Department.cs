using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Organization.Entities;

public class Department : AggregateRootBase
{
    public Guid UnitId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public Guid? ResponsibleId { get; private set; }

    protected Department() { }

    public Department(Guid unitId, string name, string? description, Guid? responsibleId)
    {
        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("ID da unidade é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do departamento é obrigatório.");
        }

        UnitId = unitId;
        Name = name.Trim();
        Description = description?.Trim();
        ResponsibleId = responsibleId;
    }

    public void Update(string name, string? description, Guid? responsibleId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome do departamento é obrigatório.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        ResponsibleId = responsibleId;
        MarkAsUpdated();
    }
}