using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.modules.IdentityAccess.Entities;

public class RolePermission : AggregateRootBase
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    protected RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("O ID da função é obrigatório.");
        }
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("O ID da permissão é obrigatório.");
        }

        RoleId = roleId;
        PermissionId = permissionId;
    }

    public void Update(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("O ID da função é obrigatório.");
        }
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("O ID da permissão é obrigatório.");
        }

        RoleId = roleId;
        PermissionId = permissionId;
        MarkAsUpdated();
    }
}