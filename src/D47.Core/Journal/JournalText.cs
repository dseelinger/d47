using System.Text;
using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>Reading names and symbols out of journal fields, shared by the sentence and the reading.</summary>
internal static class JournalText
{
    /// <summary>The localised name where Elite supplies one, then the raw.</summary>
    public static string? Named(JsonElement raw, string property) =>
        Blank(raw.String(property + "_Localised")) ?? Blank(Symbol(raw.String(property)));

    /// <summary>
    /// A bare symbol made readable where no localised name came with it: Frontier wraps some in
    /// <c>$name;</c>.
    /// </summary>
    public static string? Symbol(string? value)
    {
        if (value is not { Length: > 0 })
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith('$'))
        {
            trimmed = trimmed.TrimEnd(';')[1..];

            if (trimmed.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^5];
            }
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary><c>FSSDiscoveryScan</c> into <c>FSS Discovery Scan</c>.</summary>
    public static string Spaced(string kind)
    {
        var said = new StringBuilder(kind.Length + 8);

        for (var i = 0; i < kind.Length; i++)
        {
            // Never a second space where there already is one.
            if (i > 0
                && char.IsUpper(kind[i])
                && !char.IsWhiteSpace(kind[i - 1])
                && kind[i - 1] != '_'
                && (!char.IsUpper(kind[i - 1]) || (i + 1 < kind.Length && char.IsLower(kind[i + 1]))))
            {
                said.Append(' ');
            }

            said.Append(kind[i]);
        }

        return said.ToString();
    }

    /// <summary>Whether a message is Frontier's own string: a <c>$</c>-key with a localised form, which no player typed.</summary>
    public static bool IsFrontiersString(JsonElement raw) =>
        raw.String("Message") is { Length: > 1 } key
        && key[0] == '$'
        && raw.String("Message_Localised") is { Length: > 0 };
}
