using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public class Role : AggregateRootBase
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    protected Role() { }

    public Role(string code, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Código da função é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da função é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Descrição da função é obrigatória.");
        }

        Code = code.Trim();
        Name = name.Trim();
        Description = description.Trim();
    }

    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome da função é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Descrição da função é obrigatória.");
        }

        Name = name.Trim();
        Description = description.Trim();
        MarkAsUpdated();
    }
}