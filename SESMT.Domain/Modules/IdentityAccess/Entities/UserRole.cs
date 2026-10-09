using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.IdentityAccess.Entities;

public class UserRole : AggregateRootBase
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? CompanyId { get; private set; }

    protected UserRole() { }

    public UserRole(Guid userId, Guid roleId, Guid? companyId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("O ID do usuário é obrigatório.");
        }
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("O ID da função é obrigatório.");
        }

        UserId = userId;
        RoleId = roleId;
        CompanyId = companyId;
    }

    public void Update(Guid userId, Guid roleId, Guid? companyId = null )
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("O ID do usuário é obrigatório.");
        }
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("O ID da função é obrigatório.");
        }

        UserId = userId;
        RoleId = roleId;
        CompanyId = companyId;
        MarkAsUpdated();
    }
}