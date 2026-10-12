using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public class Permission : AggregateRootBase
{
    public string Resource { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    protected Permission() { }

    public Permission(string resource, string action, string description)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("Recurso da permissão é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Ação da permissão é obrigatória.");
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Descrição da permissão é obrigatória.");
        }

        Resource = resource.Trim();
        Action = action.Trim();
        Description = description.Trim();
    }

    public void Update(string resource, string action, string description)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("Recurso da permissão é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Ação da permissão é obrigatória.");
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Descrição da permissão é obrigatória.");
        }

        Resource = resource.Trim();
        Action = action.Trim();
        Description = description.Trim();
        MarkAsUpdated();
    }
}