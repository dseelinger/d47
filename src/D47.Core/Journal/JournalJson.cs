using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// Reading fields out of journal JSON, in one vocabulary shared by every folder in this namespace.
/// </summary>
public static class JournalJson
{
    public static string? String(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// False for a missing flag, because every boolean the journal writes is one Elite omits when it is
    /// false — Docked, OnFoot, SRV.
    /// </summary>
    public static bool Bool(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.True;

    public static int? Int(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    public static long? Long(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : null;

    public static double? Double(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;

    public static JsonElement? Object(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    /// <summary>The elements of an array property, or empty when it is absent or is not an array.</summary>
    public static IEnumerable<JsonElement> Items(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    /// <summary>The player-facing name where Elite supplies one, falling back to the internal symbol.</summary>
    public static string? Named(this JsonElement element, string property) =>
        element.String(property + "_Localised") ?? element.String(property);

    /// <summary>A name Elite left unlocalised, cased the way the localised ones are cased.</summary>
    public static string? Spoken(string? name) =>
        name is { Length: > 0 } && char.IsLower(name[0])
            ? char.ToUpperInvariant(name[0]) + name[1..]
            : name;

    /// <summary>The spoken name of a property.</summary>
    public static string? Spoken(this JsonElement element, string property) =>
        Spoken(element.Named(property));

    /// <summary>
    /// The internal symbol, folded so that two events which spell the same thing differently join.
    /// </summary>
    public static string? Symbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var symbol = value.Trim();

        if (symbol.StartsWith('$'))
        {
            symbol = symbol[1..];
        }

        if (symbol.EndsWith(';'))
        {
            symbol = symbol[..^1];
        }

        if (symbol.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
        {
            symbol = symbol[..^"_name".Length];
        }

        return symbol.Length == 0 ? null : symbol.ToLowerInvariant();
    }

    /// <summary>The folded symbol of a property, for joining.</summary>
    public static string? Symbol(this JsonElement element, string property) =>
        Symbol(element.String(property));
}
