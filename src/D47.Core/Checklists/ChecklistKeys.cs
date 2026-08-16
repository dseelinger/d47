using System.Globalization;
using System.Text;

namespace D47.Core.Checklists;

/// <summary>
/// How an item comes by the key that is its identity (list.md Phase 17, "item identity is the
/// load-bearing decision").
/// <para>
/// <b>Never positional.</b> A key derived from where an item sits in a list makes every revision
/// read as <i>everything removed, everything added</i>. A key derived from the item's own content
/// makes a revision a diff, which is the whole point.
/// </para>
/// <para>
/// <b>And never random, and never stamped.</b> Core owns no thread and reads no clock
/// (architecture.md §8), and a key drawn from either would make the replay harness produce a
/// different document every run. An authored key is therefore the lowest number not already in
/// use, which is deterministic, survives an edit to the wording, and reads fine to somebody with
/// the file open in a text editor.
/// </para>
/// </summary>
public static class ChecklistKeys
{
    /// <summary>What an authored key starts with, so the two kinds are told apart by eye.</summary>
    public const string NotePrefix = "note-";

    /// <summary>
    /// The next authored key in a scope: the lowest positive integer nothing there is already
    /// using, <b>counting tombstones</b>. Reusing the key of an item somebody abandoned would
    /// silently graft the old item's history onto a new one.
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
    /// A derived item's key: <b>slot plus intent</b> for a ship, <b>body or orbital slot plus
    /// facility type</b> for a system — exactly as list.md states it.
    /// <para>
    /// The grade is in the key, and that is deliberate. A wildcard-grade intent and a grade 5
    /// intent on the same slot are two different things to want, and a plan that changes one into
    /// the other has changed its mind about that slot.
    /// </para>
    /// </summary>
    public static string For(ChecklistIntent intent)
    {
        var parts = new List<string>
        {
            Kind(intent.Kind),
            Compact(intent.Subject),
        };

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
    /// One word for each intent kind, so a key says what shape of thing it is without anything
    /// having to parse the rest of it.
    /// </summary>
    private static string Kind(ChecklistIntentKind kind) => kind switch
    {
        ChecklistIntentKind.Blueprint => "bp",
        ChecklistIntentKind.Experimental => "xp",
        ChecklistIntentKind.Module => "mod",
        ChecklistIntentKind.EngineerAccess => "eng",
        ChecklistIntentKind.Facility => "fac",
        ChecklistIntentKind.Commodity => "com",
        _ => "x",
    };

    /// <summary>
    /// Text reduced to what identifies it and nothing else: lower case, letters and digits, and
    /// <b>no separators at all</b>.
    /// <para>
    /// Deliberately lossy and deliberately stable. "MainEngines", "main engines" and "Main
    /// Engines" are one slot, and a key that told them apart would make a plan restated in a
    /// differently-worded conversation look like a plan rebuilt from nothing — every item
    /// tombstoned and an identical set opened beside it. That is the exact failure item identity
    /// exists to prevent, and it is why this drops the separator that <see cref="Slug"/> keeps.
    /// </para>
    /// </summary>
    public static string Compact(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : new string([.. text.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    /// <summary>
    /// The same reduction with single hyphens kept, for anything a person reads rather than
    /// anything that has to compare equal.
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
