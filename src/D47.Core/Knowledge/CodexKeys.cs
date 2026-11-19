using System.Globalization;
using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>
/// Codex keys: a Canonn entry's key is its EntryID in decimal; a d47 kind's key starts with a lowercase letter and uses
/// lowercase letters, digits, <c>_</c> and <c>.</c>, never <c>..</c>.
/// </summary>
public static partial class CodexKeys
{
    /// <summary>The key of a journal EntryID.</summary>
    public static string Of(long entryId) => entryId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The EntryID a numeric key names; null for a kind key or a string that is not a key.</summary>
    public static long? EntryIdOf(string? key) =>
        key is { Length: > 0 }
        && char.IsAsciiDigit(key[0])
        && long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0
        && string.Equals(Of(id), key, StringComparison.Ordinal)
            ? id
            : null;

    /// <summary>Whether <paramref name="key"/> is a d47 kind's key.</summary>
    public static bool IsKind(string? key) => key is not null && KindShape().IsMatch(key) && !key.Contains("..", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="key"/> is a codex key of either form.</summary>
    public static bool IsValid(string? key) => EntryIdOf(key) is not null || IsKind(key);

    [GeneratedRegex(@"^[a-z][a-z0-9_.]*\z")]
    private static partial Regex KindShape();
}
