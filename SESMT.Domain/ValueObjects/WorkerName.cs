namespace SESMT.Domain.ValueObjects;

/// <summary>
/// Representa o nome de um trabalhador dentro do domínio.
/// O valor é normalizado e validado pelo próprio Value Object.
/// </summary>
public sealed class WorkerName : IEquatable<WorkerName>
{
    public string Value { get; }

    public WorkerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "O nome do trabalhador não pode ser vazio.");
        }

        Value = value.Trim();
    }

    public bool Equals(WorkerName? other)
    {
        if (other is null)
            return false;

        return Value.Equals(
            other.Value,
            StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as WorkerName);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }

    public override string ToString()
    {
        return Value;
    }

    public static bool operator ==(
        WorkerName? left,
        WorkerName? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(
        WorkerName? left,
        WorkerName? right)
    {
        return !Equals(left, right);
    }
}