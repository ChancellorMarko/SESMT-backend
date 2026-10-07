namespace SESMT.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "O e-mail não pode ser vazio.");
        }

        Value = value.Trim();
    }

    public bool Equals(Email? other)
    {
        if (other is null)
            return false;

        return Value.Equals(
            other.Value,
            StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Email);
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
        Email? left,
        Email? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(
        Email? left,
        Email? right)
    {
        return !Equals(left, right);
    }
}