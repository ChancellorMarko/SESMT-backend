namespace SESMT.Domain.ValueObjects;

public sealed class Cpf : IEquatable<Cpf>
{
    public string Value { get; }

    public Cpf(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("O CPF não pode ser vazio.");

        var normalized = Normalize(value);

        if (!IsValid(normalized))
            throw new ArgumentException("CPF inválido.");

        Value = normalized;
    }

    public static string Normalize(string value) =>
        new string(value.Where(char.IsDigit).ToArray());

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var digits = Normalize(value);

        if (digits.Length != 11)
            return false;

        if (digits.All(c => c == digits[0]))
            return false;

        var temp = digits[..9];
        var digit1 = CalculateCheckDigit(temp);
        var digit2 = CalculateCheckDigit(temp + digit1);

        return digits[9] == digit1 && digits[10] == digit2;
    }

    private static char CalculateCheckDigit(string digits)
    {
        var sum = 0;
        var multiplier = digits.Length + 1;

        foreach (var c in digits)
        {
            sum += (c - '0') * multiplier;
            multiplier--;
        }

        var remainder = sum % 11;
        var result = remainder < 2 ? 0 : 11 - remainder;
        return (char)('0' + result);
    }

    public string ToFormattedString() =>
        Value.Length == 11
            ? $"{Value[..3]}.{Value[3..6]}.{Value[6..9]}-{Value[9..]}"
            : Value;

    public string ToMaskedString() =>
        Value.Length == 11 ? $"***.***.***-{Value[^2..]}" : "***.***.***-**";

    public override string ToString() => Value;

    public bool Equals(Cpf? other) =>
        other is not null && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Cpf);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(Cpf? left, Cpf? right) => Equals(left, right);
    public static bool operator !=(Cpf? left, Cpf? right) => !Equals(left, right);
}
