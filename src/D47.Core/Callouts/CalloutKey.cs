using System.Globalization;

namespace D47.Core.Callouts;

/// <summary>Reads and writes announcement keys of the form <c>prefix + head + ("." + number)*</c>; the head may hold dots.</summary>
public static class CalloutKey
{
    /// <summary>The head and the part after its last dot, or null when the key lacks <paramref name="prefix"/> or a non-empty head.</summary>
    public static (string Head, string Tail)? Cut(string? key, string prefix)
    {
        if (key is null || !key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[prefix.Length..];
        var dot = rest.LastIndexOf('.');

        return dot > 0 ? (rest[..dot], rest[(dot + 1)..]) : null;
    }

    /// <summary>The head and the number after its last dot, or null when the key does not parse.</summary>
    public static (string Head, int Number)? Split(string? key, string prefix, NumberStyles style) =>
        Cut(key, prefix) is var (head, tail) && int.TryParse(tail, style, CultureInfo.InvariantCulture, out var number)
            ? (head, number)
            : null;

    public static string Join(string prefix, string head, params int[] tail) =>
        string.Concat(tail.Select(number => $".{number.ToString(CultureInfo.InvariantCulture)}").Prepend(prefix + head));
}
