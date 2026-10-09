namespace SESMT.Domain.ValueObjects;

public sealed class Cpf : IEquatable<Cpf>
{
    public string Value { get; }

    public Cpf(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "O CPF não pode ser vazio.");
        }

        Value = value.Trim();
    }

    public bool Equals(Cpf? other)
    {
        if (other is null)
            return false;

        return Value.Equals(
            other.Value,
            StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Cpf);
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
        Cpf? left,
        Cpf? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(
        Cpf? left,
        Cpf? right)
    {
        return !Equals(left, right);
    }
}