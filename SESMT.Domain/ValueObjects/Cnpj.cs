namespace SESMT.Domain.ValueObjects;

public sealed class Cnpj : IEquatable<Cnpj>
{
    public string Value { get; }

    public Cnpj(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("O CNPJ não pode ser vazio.");

        var normalized = Normalize(value);

        if (!IsValid(normalized))
            throw new ArgumentException("CNPJ inválido.");

        Value = normalized;
    }

    /// <summary>
    /// Normaliza para maiúsculo, removendo pontuação. Suporta o novo CNPJ alfanumérico (IN RFB 2.229/2024).
    /// </summary>
    public static string Normalize(string value) =>
        new string(value
            .Where(c => char.IsLetterOrDigit(c))
            .Select(char.ToUpperInvariant)
            .ToArray());

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = Normalize(value);

        if (normalized.Length != 14)
            return false;

        if (normalized.All(c => c == normalized[0]))
            return false;

        // DVs são sempre numéricos
        if (!char.IsDigit(normalized[12]) || !char.IsDigit(normalized[13]))
            return false;

        var baseCnpj = normalized[..12];
        var digit1 = CalculateCheckDigit(baseCnpj);
        var digit2 = CalculateCheckDigit(baseCnpj + digit1);

        return normalized[12] == digit1 && normalized[13] == digit2;
    }

    private static char CalculateCheckDigit(string baseChars)
    {
        int[] weights = baseChars.Length == 12
            ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
            : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var sum = 0;
        for (var i = 0; i < baseChars.Length; i++)
        {
            // ASCII-48: '0'→0 ... '9'→9, 'A'→17 ... 'Z'→42
            var numericValue = baseChars[i] - 48;
            sum += numericValue * weights[i];
        }

        var remainder = sum % 11;
        var result = remainder < 2 ? 0 : 11 - remainder;
        return (char)('0' + result);
    }

    public string ToFormattedString() =>
        Value.Length == 14
            ? $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}"
            : Value;

    public override string ToString() => Value;

    public bool Equals(Cnpj? other) =>
        other is not null && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Cnpj);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(Cnpj? left, Cnpj? right) => Equals(left, right);
    public static bool operator !=(Cnpj? left, Cnpj? right) => !Equals(left, right);
}
