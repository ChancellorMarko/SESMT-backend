using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.modules.IdentityAccess.Entities;

public class UserScope : AggregateRootBase
{
    public Guid UserId { get; private set; }
    public string ScopeType { get; private set; }
    public Guid? ReferenceId { get; private set; }

    protected UserScope() { }

    public UserScope(Guid userId, string scopeType)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("O ID do usuário é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(scopeType))
        {
            throw new ArgumentException("O tipo de escopo é obrigatório.");
        }

        UserId = userId;
        ScopeType = scopeType.Trim();
    }

    public void Update(Guid userId, string scopeType, Guid? referenceId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("O ID do usuário é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(scopeType))
        {
            throw new ArgumentException("O tipo de escopo é obrigatório.");
        }

        UserId = userId;
        ScopeType = scopeType.Trim();
        ReferenceId = referenceId;
        MarkAsUpdated();
    }
}