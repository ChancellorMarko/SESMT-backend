namespace SESMT.Domain.ValueObjects;

/// <summary>
/// Nome de uma pessoa no sistema. Normaliza espaços e valida caracteres permitidos.
/// </summary>
public sealed class WorkerName : IEquatable<WorkerName>
{
    public string Value { get; }

    public WorkerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("O nome não pode ser vazio.");

        var normalized = Normalize(value);

        if (normalized.Length < 2)
            throw new ArgumentException("O nome deve ter pelo menos 2 caracteres.");

        if (normalized.Any(char.IsDigit))
            throw new ArgumentException("O nome não pode conter números.");

        Value = normalized;
    }

    /// <summary>
    /// Normaliza: trim, remove espaços múltiplos.
    /// </summary>
    public static string Normalize(string value) =>
        string.Join(' ',
            value.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public override string ToString() => Value;

    public bool Equals(WorkerName? other) =>
        other is not null && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as WorkerName);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(WorkerName? left, WorkerName? right) => Equals(left, right);
    public static bool operator !=(WorkerName? left, WorkerName? right) => !Equals(left, right);
}
