using System.Globalization;
using System.Text;

namespace D47.Core.Checklists;

/// <summary>
/// How an item comes by the key that is its identity (Phase 17, "item identity is the load-bearing
/// decision").
/// </summary>
public static class ChecklistKeys
{
    /// <summary>What an authored key starts with, so the two kinds are told apart by eye.</summary>
    public const string NotePrefix = "note-";

    /// <summary>
    /// The next authored key in a scope: the lowest positive integer nothing there is already using,
    /// counting tombstones.
    /// </summary>
    public static string Note(IEnumerable<ChecklistItem> existing)
    {
        var taken = new HashSet<int>();

        foreach (var item in existing)
        {
            if (item.Key.StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(
                    item.Key[NotePrefix.Length..], CultureInfo.InvariantCulture, out var number))
            {
                taken.Add(number);
            }
        }

        var next = 1;

        while (taken.Contains(next))
        {
            next++;
        }

        return NotePrefix + next.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A derived item's key: slot plus intent for a ship, body or orbital slot plus facility type for a
    /// system — exactly as the phase states it.
    /// </summary>
    public static string For(ChecklistIntent intent)
    {
        var parts = new List<string>
        {
            Kind(intent.Kind),
            Compact(intent.Subject),
        };

        if (SlotShaped(intent.Kind))
        {
            return string.Join('/', parts);
        }

        if (Compact(intent.Detail) is { Length: > 0 } detail)
        {
            parts.Add(detail);
        }

        if (intent.Grade is { } grade)
        {
            parts.Add("g" + grade.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join('/', parts);
    }

    /// <summary>
    /// Whether an intent's subject is a place on a hull that holds exactly one of this kind of thing at
    /// a time — which is what makes the slot, rather than the content, the identity.
    /// </summary>
    public static bool SlotShaped(ChecklistIntentKind kind) => kind is
        ChecklistIntentKind.Blueprint or
        ChecklistIntentKind.Experimental or
        ChecklistIntentKind.Module;

    /// <summary>
    /// One word for each intent kind, so a key says what shape of thing it is without anything having
    /// to parse the rest of it.
    /// </summary>
    private static string Kind(ChecklistIntentKind kind) => kind switch
    {
        ChecklistIntentKind.Blueprint => "bp",
        ChecklistIntentKind.Experimental => "xp",
        ChecklistIntentKind.Module => "mod",
        ChecklistIntentKind.EngineerAccess => "eng",
        ChecklistIntentKind.Facility => "fac",
        ChecklistIntentKind.Commodity => "com",
        ChecklistIntentKind.Grade => "grd",
        ChecklistIntentKind.Modification => "fit",
        _ => "x",
    };

    /// <summary>
    /// Text reduced to what identifies it and nothing else: lower case, letters and digits, and no
    /// separators at all.
    /// </summary>
    public static string Compact(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : new string([.. text.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    /// <summary>
    /// The same reduction with single hyphens kept, for anything a person reads rather than anything
    /// that has to compare equal.
    /// </summary>
    public static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var built = new StringBuilder(text.Length);

        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                built.Append(character);
            }
            else if (built.Length > 0 && built[^1] != '-')
            {
                built.Append('-');
            }
        }

        return built.ToString().Trim('-');
    }
}
