using System.Net.Mail;

namespace SESMT.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("O e-mail não pode ser vazio.");

        var normalized = value.Trim().ToLowerInvariant();

        if (!IsValid(normalized))
            throw new ArgumentException("E-mail inválido.");

        Value = normalized;
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Length > 254)
            return false;

        return MailAddress.TryCreate(value, out _);
    }

    public override string ToString() => Value;

    public bool Equals(Email? other) =>
        other is not null && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(Email? left, Email? right) => Equals(left, right);
    public static bool operator !=(Email? left, Email? right) => !Equals(left, right);
}
