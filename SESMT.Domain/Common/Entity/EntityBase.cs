namespace SESMT.Domain.Common.Entity;

public class EntityBase
{
    public Guid Id { get; protected set; }
    public bool Active { get; protected set; } = true;
    public DateTime CreatedAt { get; protected set; }
    public Guid? CreatedBy { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public Guid? DeletedBy { get; protected set; }

    protected EntityBase()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsUpdated(Guid? updatedBy = null)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void MarkAsDeleted(Guid? deletedBy = null)
    {
        Active = false;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }
}