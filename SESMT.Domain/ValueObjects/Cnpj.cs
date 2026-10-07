namespace SESMT.Domain.ValueObjects;

public sealed class Cnpj : IEquatable<Cnpj>
{
    public string Value { get; }

    public Cnpj(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "O CNPJ não pode ser vazio.");
        }

        Value = value.Trim();
    }

    public bool Equals(Cnpj? other)
    {
        if (other is null)
            return false;

        return Value.Equals(
            other.Value,
            StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Cnpj);
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
        Cnpj? left,
        Cnpj? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(
        Cnpj? left,
        Cnpj? right)
    {
        return !Equals(left, right);
    }
}