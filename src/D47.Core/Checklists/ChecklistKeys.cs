using System.Globalization;
using System.Text;

namespace D47.Core.Checklists;

/// <summary>
/// How an item comes by the key that is its identity (Phase 17, "item identity is the
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
    /// facility type</b> for a system — exactly as the phase states it.
    /// <para>
    /// <b>A slot-shaped intent keys on the slot alone, and nothing about what is wanted in it</b>
    /// (Phase 26, "A plan is keyed to its slot"). This used to carry the blueprint and the
    /// grade as well, and that was right for as long as items were regenerated from scratch on
    /// every evaluation: two conversations settling on different things wanted two items, and the
    /// diff sorted it out. <b>It does not survive a plan the Commander edits.</b> Changing a slot
    /// from a long-range pulse laser to an overcharged multi-cannon is not a delete and an add —
    /// it is still the third hardpoint on the same hull, and keying on the content made an edit
    /// read as a fortnight of progress abandoned and an identical-looking new item opened beside
    /// the tombstone.
    /// </para>
    /// <para>
    /// It needs no counter and no tombstone bookkeeping, because <b>a slot exists as long as the
    /// hull does</b>. And it is what makes <em>one build per ship</em> a decision rather than an
    /// accident: comparing a combat fit against an exploration fit for the same hull would need
    /// two items in one slot, and there is deliberately no way to spell that.
    /// </para>
    /// <para>
    /// <b>Only the slot-shaped kinds.</b> A suit carries several modifications at once and a
    /// construction site several commodities, so those still key on what they are about — see
    /// <see cref="SlotShaped"/>.
    /// </para>
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
    /// Whether an intent's subject is a place on a hull that holds exactly one of this kind of
    /// thing at a time — which is what makes the slot, rather than the content, the identity.
    /// <para>
    /// Three of them, and the list is closed by the game rather than by a judgement: a ship slot
    /// holds one module, that module carries at most one blueprint, and it carries at most one
    /// experimental effect. Everything else on <see cref="ChecklistIntentKind"/> is one-to-many —
    /// a suit takes several modifications, a site wants several commodities, an engineer is not a
    /// place at all — so for those the content is still what tells two items apart.
    /// </para>
    /// </summary>
    public static bool SlotShaped(ChecklistIntentKind kind) => kind is
        ChecklistIntentKind.Blueprint or
        ChecklistIntentKind.Experimental or
        ChecklistIntentKind.Module;

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
        ChecklistIntentKind.Grade => "grd",
        ChecklistIntentKind.Modification => "fit",
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
