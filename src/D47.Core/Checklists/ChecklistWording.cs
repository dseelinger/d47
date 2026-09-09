using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// A checklist line as a Commander reads it: the module the slot actually holds, and the ship it is on
/// (reported 2026-08-21 — "These should mention the ship and module they happened on").
/// </summary>
public static class ChecklistWording
{
    /// <summary>
    /// What the line says, with a slot resolved to the module sitting in it — <c>Slot01_Size7</c>
    /// becoming "7A Shield Generator".
    /// </summary>
    public static string Said(ChecklistItem item, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Intent is not { } intent)
        {
            return item.Text;
        }

        // **An unlock line says what the invitation asks for** (<a
        // href=".com/dseelinger/d47/issues/22">#22</a>).
        if (intent.Kind == ChecklistIntentKind.EngineerAccess && (intent.Grade ?? 1) <= 1)
        {
            return Invitation(item.Text, intent.Subject);
        }

        if (!ChecklistKeys.SlotShaped(intent.Kind) || item.Scope.Group != ChecklistGroup.Ship)
        {
            return item.Text;
        }

        var said = InSlot(item, intent.Subject, state);

        // The subject as the plan spelled it, which is what the text was built from.
        return said is null || said == intent.Subject
            ? item.Text
            : Swap(item.Text, intent.Subject, said);
    }

    /// <summary>What the invitation asks for, on the end of an unlock line (#22).</summary>
    private static string Invitation(string text, string engineer)
    {
        if (EngineerDirectory.ByName(engineer) is not { } found)
        {
            return text;
        }

        if (found.Unlock is { Length: > 0 } asks)
        {
            return $"{text} — {Sentence(asks)}";
        }

        // Short on purpose.
        return found.Meeting is { Length: > 0 }
            ? $"{text} — no invitation task on record"
            : text;
    }

    /// <summary>
    /// Lower-cased where it is a whole sentence, so the invitation reads as a clause on the end of one.
    /// </summary>
    private static string Sentence(string said) =>
        said.Length > 1 && char.IsUpper(said[0]) && !char.IsUpper(said[1])
            ? char.ToLowerInvariant(said[0]) + said[1..].TrimEnd('.')
            : said.TrimEnd('.');

    /// <summary>
    /// The whole sentence, ship and all — "Grade 5 Reinforced Shields on 7A Shield Generator on
    /// Flamebrand (Anaconda)".
    /// </summary>
    public static string Line(ChecklistItem item, CommanderGameState? state)
    {
        var said = Said(item, state);

        return Ship(item.Scope, item.Hull, state) is { } ship ? $"{said} on {ship}" : said;
    }

    /// <summary>
    /// The line as it is spoken: <see cref="Line"/>, less the ship when the ship is the one being
    /// flown.
    /// </summary>
    public static string Aloud(ChecklistItem item, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(item);

        return state?.Ship is { } loadout && ChecklistEvaluator.IsActive(item.Scope, loadout)
            ? Said(item, state)
            : Line(item, state);
    }

    /// <summary>
    /// What list this is, for the caption under the line and for the heading over a group of them: the
    /// ship as its Commander names it, and otherwise the scope's own words.
    /// </summary>
    public static string Where(ChecklistItem item, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Where(item.Scope, item.Hull, state);
    }

    /// <inheritdoc cref="Where(ChecklistItem, CommanderGameState?)"/>
    public static string Where(ChecklistScope scope, string? hull, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Ship(scope, hull, state) ?? scope.ToString();
    }

    /// <summary>The ship a scope is about — "Flamebrand (Anaconda)" — or null where it is about no ship.</summary>
    public static string? Ship(ChecklistScope scope, string? hull, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.Group != ChecklistGroup.Ship
            || !int.TryParse(scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shipId))
        {
            return null;
        }

        if (state?.Loadouts.For(shipId)?.Loadout is { } loadout)
        {
            return Named(loadout.Name, loadout.TypeSaid) ?? Anonymous(hull, shipId);
        }

        if (state?.Fleet.Ships.FirstOrDefault(ship => ship.ShipId == shipId) is { } stored)
        {
            return Named(stored.Name, HullName(stored.Type)) ?? Anonymous(hull, shipId);
        }

        return Anonymous(hull, shipId);
    }

    /// <summary>
    /// One item's subject as a Commander says it, for a sentence that names slots rather than draws
    /// lines (#154).
    /// </summary>
    public static string? Subject(ChecklistItem item, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Intent is not { } intent)
        {
            return item.Text;
        }

        if (!ChecklistKeys.SlotShaped(intent.Kind) || item.Scope.Group != ChecklistGroup.Ship)
        {
            return intent.Subject;
        }

        return InSlot(item, intent.Subject, state) ?? Subject(intent.Subject, item.Hull);
    }

    /// <summary>
    /// A slot with no item of its own to describe it — the ones a revision is dropping, which have no
    /// entry in what it proposes.
    /// </summary>
    public static string? Subject(string subject, string? hull) =>
        hull is { Length: > 0 } type ? EliteSpecifications.Slot(type, subject)?.Describe() : null;

    /// <summary>The module in a slot, as a Commander says it — "7A Shield Generator".</summary>
    private static bool Twinned(ShipLoadout loadout, ShipModule module) =>
        module.Item is { Length: > 0 } item
        && loadout.Modules.Count(other =>
            string.Equals(other.Item, item, StringComparison.OrdinalIgnoreCase)) > 1;

    private static string? InSlot(ChecklistItem item, string subject, CommanderGameState? state)
    {
        if (state is null
            || !int.TryParse(
                item.Scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shipId))
        {
            return null;
        }

        // The remembered loadout rather than the ship being flown, so a plan for the Cutter in another dock
        // still names its modules (Phase 37 remembered them for exactly this).
        if (state.Loadouts.For(shipId)?.Loadout is not { } loadout)
        {
            return null;
        }

        // The same matching the verdict uses, so the line and the verdict under it cannot come to different
        // conclusions about which module the item is about.
        if (ChecklistEvaluator.Fitted(loadout, subject) is { } module)
        {
            var described = ChecklistEvaluator.Describe(module);

            // **And the mounting point back where the type alone cannot tell two lines apart**
            // (docs/plans/change-requests.md 44).
            if (Twinned(loadout, module)
                && EliteSpecifications.Slot(loadout.Type ?? item.Hull, subject)?.Describe() is { } mount)
            {
                return $"{described} in {mount}";
            }

            return described;
        }

        // Nothing fitted, so the next best answer is what the plan says is going there (asked for
        // 2026-08-24). The module type, never the mounting point. Reported as "Utility Mount 8 and
        // Compartment 4 don't tell me the module type", and d47 knew all along — the ship plan stores the
        // module beside the blueprint and this method had never been shown it.

        if (item.Intent?.Module is { Length: > 0 } planned)
        {
            return planned;
        }

        // And where even the plan does not say — a slot the Commander asked for engineering on without
        // choosing what goes in it — the slot's own name is all there is.
        return EliteSpecifications.Slot(loadout.Type ?? item.Hull, subject)?.Describe();
    }

    /// <summary>"Flamebrand (Anaconda)", or as much of it as is actually known.</summary>
    private static string? Named(string? name, string? hull) => (name, hull) switch
    {
        ({ Length: > 0 } called, { Length: > 0 } type) => $"{called} ({type})",
        ({ Length: > 0 } called, _) => called,
        (_, { Length: > 0 } type) => type,
        _ => null,
    };

    /// <summary>A ship nothing has named: its hull where the item stored one, and the id either way.</summary>
    private static string Anonymous(string? hull, int shipId)
    {
        var said = $"ship {shipId.ToString(CultureInfo.InvariantCulture)}";

        return HullName(hull) is { Length: > 0 } type ? $"{type} ({said})" : said;
    }

    /// <summary>The hull as it should be said.</summary>
    private static string? HullName(string? hull) =>
        hull is not { Length: > 0 } ? null : EliteSpecifications.HullSaid(hull);

    /// <summary>
    /// The last occurrence of <paramref name="what"/> replaced, which is the one the slot is in: every
    /// plan wording puts the slot at the end, after the blueprint and before the engineer.
    /// </summary>
    private static string Swap(string text, string what, string with)
    {
        var at = text.LastIndexOf(what, StringComparison.OrdinalIgnoreCase);

        return at < 0 ? text : string.Concat(text.AsSpan(0, at), with, text.AsSpan(at + what.Length));
    }
}
