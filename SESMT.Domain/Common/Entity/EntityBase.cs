namespace SESMT.Domain.Common.Entity;

public abstract class EntityBase
{
    public Guid Id { get; protected set; }

    protected EntityBase()
    {
        Id = Guid.NewGuid();
    }

    protected EntityBase(Guid id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not EntityBase other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(EntityBase? left, EntityBase? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Id == right.Id;
    }

    public static bool operator !=(EntityBase? left, EntityBase? right)
    {
        return !(left == right);
    }
}