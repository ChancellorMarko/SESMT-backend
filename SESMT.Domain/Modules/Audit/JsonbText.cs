using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SESMT.Domain.Modules.Audit;

/// <summary>
/// Reproduces PostgreSQL's <c>jsonb</c> text output, so a hash computed over this text still matches after the value
/// is stored in a <c>jsonb</c> column and read back. Rules: object keys ordered by byte length and then by bytes, the
/// last duplicate key wins, <c>", "</c> and <c>": "</c> separators, only the escapes PostgreSQL emits (non-ASCII text stays
/// literal) and numbers without exponent keeping their scale. Text that is not valid JSON is returned unchanged.
/// </summary>
public static class JsonbText
{
    public static string? Canonicalize(string? json)
    {
        if (json is null) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
            var output = new StringBuilder(json.Length);
            Write(document.RootElement, output);
            return output.ToString();
        }
        catch (JsonException) { return json; }
    }

    private static void Write(JsonElement element, StringBuilder output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject()) properties[property.Name] = property.Value;
                output.Append('{');
                var first = true;
                foreach (var (name, value) in properties.OrderBy(p => p.Key, KeyOrder.Instance))
                {
                    if (!first) output.Append(", ");
                    first = false;
                    WriteString(name, output); output.Append(": "); Write(value, output);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                var index = 0;
                foreach (var item in element.EnumerateArray()) { if (index++ > 0) output.Append(", "); Write(item, output); }
                output.Append(']');
                break;
            case JsonValueKind.String: WriteString(element.GetString()!, output); break;
            case JsonValueKind.Number: output.Append(Number(element.GetRawText())); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            default: output.Append("null"); break;
        }
    }

    // PostgreSQL escape_json: quote, backslash and control characters; everything else literal.
    private static void WriteString(string value, StringBuilder output)
    {
        output.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\f': output.Append("\\f"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (c < ' ') output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else output.Append(c);
                    break;
            }
        }
        output.Append('"');
    }

    // numeric_out: plain notation, display scale = fraction digits minus exponent (never negative).
    private static string Number(string raw)
    {
        var negative = raw.StartsWith('-');
        var text = negative ? raw[1..] : raw;
        var exponentAt = text.IndexOfAny(['e', 'E']);
        var exponent = exponentAt < 0 ? 0 : int.Parse(text[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var mantissa = exponentAt < 0 ? text : text[..exponentAt];
        var point = mantissa.IndexOf('.');
        var integerPart = point < 0 ? mantissa : mantissa[..point];
        var fraction = point < 0 ? "" : mantissa[(point + 1)..];
        var scale = Math.Max(fraction.Length - exponent, 0);
        var digits = integerPart + fraction;
        var integerLength = integerPart.Length + exponent;
        string whole, decimals;
        if (integerLength <= 0) { whole = "0"; decimals = new string('0', -integerLength) + digits; }
        else if (integerLength >= digits.Length) { whole = digits + new string('0', integerLength - digits.Length); decimals = ""; }
        else { whole = digits[..integerLength]; decimals = digits[integerLength..]; }
        whole = whole.TrimStart('0'); if (whole.Length == 0) whole = "0";
        decimals = decimals.Length >= scale ? decimals[..scale] : decimals + new string('0', scale - decimals.Length);
        var zero = whole == "0" && decimals.All(d => d == '0');
        return (negative && !zero ? "-" : "") + whole + (scale > 0 ? "." + decimals : "");
    }

    // jsonb orders keys by UTF-8 byte length first, then by the bytes themselves.
    private sealed class KeyOrder : IComparer<string>
    {
        public static readonly KeyOrder Instance = new();
        public int Compare(string? x, string? y)
        {
            var a = Encoding.UTF8.GetBytes(x ?? ""); var b = Encoding.UTF8.GetBytes(y ?? "");
            return a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.AsSpan().SequenceCompareTo(b);
        }
    }
}
