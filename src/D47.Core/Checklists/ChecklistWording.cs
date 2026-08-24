using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// A checklist line as a Commander reads it: the module the slot actually holds, and the ship it
/// is on (reported 2026-08-21 — <i>"These should mention the ship and module they happened on"</i>).
/// <para>
/// <b>Ship was already the axis; only the wording was missing.</b> <see cref="ChecklistScope"/>
/// has been keyed on the journal's <c>ShipID</c> since Phase 17, and it is what makes a build
/// follow a hull through a swap. What reached the page was the key itself — <i>ship 51</i> — which
/// is d47's identifier for the ship and nobody's name for one. Three done items reading
/// <i>ship 51</i>, <i>ship 51</i>, <i>ship 53</i> say the Commander finished work on two of
/// something, and no more than that.
/// </para>
/// <para>
/// <b>Computed here, never stored on the item.</b> <see cref="ChecklistItem.Text"/> is minted when
/// a plan is adopted and is the plan's own wording — the same rule that keeps a plan's figures out
/// of the file keeps the ship's name out of it, because a Commander renames a ship and refits a
/// slot long after the line was written. So the slot is resolved against the loadout every time it
/// is drawn, and a slot d47 cannot see falls back to what was stored rather than to a guess.
/// </para>
/// <para>
/// <b>Null hull and null ship are ordinary.</b> A custom, system, suit or weapon line is about no
/// ship at all and <see cref="Ship"/> answers null for it; a ship-scoped line for a hull d47 has
/// never watched keeps the scope's own spelling. Neither is an error, and neither invents a name.
/// </para>
/// </summary>
public static class ChecklistWording
{
    /// <summary>
    /// What the line says, with a slot resolved to the module sitting in it — <c>Slot01_Size7</c>
    /// becoming "7A Shield Generator". The item's own text unchanged where nothing can be
    /// resolved.
    /// </summary>
    public static string Said(ChecklistItem item, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Intent is not { } intent
            || !ChecklistKeys.SlotShaped(intent.Kind)
            || item.Scope.Group != ChecklistGroup.Ship)
        {
            return item.Text;
        }

        var said = InSlot(item, intent.Subject, state);

        // The subject as the plan spelled it, which is what the text was built from. Compared
        // rather than assumed: an item whose wording a revision changed is left alone instead of
        // having a substring rewritten out from under it.
        return said is null || said == intent.Subject
            ? item.Text
            : Swap(item.Text, intent.Subject, said);
    }

    /// <summary>
    /// The whole sentence, ship and all — "Grade 5 Reinforced Shields on 7A Shield Generator on
    /// Flamebrand (Anaconda)".
    /// <para>
    /// For the places that print one line and no heading over it. Where the ship is already a
    /// heading, <see cref="Said"/> is the half that belongs on the line.
    /// </para>
    /// </summary>
    public static string Line(ChecklistItem item, CommanderGameState? state)
    {
        var said = Said(item, state);

        return Ship(item.Scope, item.Hull, state) is { } ship ? $"{said} on {ship}" : said;
    }

    /// <summary>
    /// The line as it is <em>spoken</em>: <see cref="Line"/>, less the ship when the ship is the
    /// one being flown.
    /// <para>
    /// <b>An amendment to the reasoning beside the spoken callout</b>, asked for 2026-08-23:
    /// <i>"I know what ship I'm in."</i> That comment argued the ship must ride the sentence
    /// because it is the one checklist line with no heading over it, in a session where three
    /// ships have been flown — which is right about every ship except the one under the Commander.
    /// So the ship is named when it is not obvious and dropped when it is, rather than always.
    /// </para>
    /// </summary>
    public static string Aloud(ChecklistItem item, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(item);

        return state?.Ship is { } loadout && ChecklistEvaluator.IsActive(item.Scope, loadout)
            ? Said(item, state)
            : Line(item, state);
    }

    /// <summary>
    /// What list this is, for the caption under the line and for the heading over a group of them:
    /// the ship as its Commander names it, and otherwise the scope's own words.
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

    /// <summary>
    /// The ship a scope is about — "Flamebrand (Anaconda)" — or null where it is about no ship.
    /// <para>
    /// <b>Three sources, best first, and none of them invented.</b> The remembered loadout knows
    /// the name the Commander typed into the ship and the hull Frontier localised; the fleet
    /// snapshot knows both for a ship parked somewhere d47 has not been aboard; and the item's own
    /// stored hull is what is left when neither has ever been seen. Only the last drops the name,
    /// and it keeps the id beside the hull so two Anacondas stay two ships.
    /// </para>
    /// </summary>
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
    /// The module in a slot, as a Commander says it — "7A Shield Generator". Null for a slot
    /// nothing is in, or one on a ship d47 has never been aboard.
    /// </summary>
    private static string? InSlot(ChecklistItem item, string subject, CommanderGameState? state)
    {
        if (state is null
            || !int.TryParse(
                item.Scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shipId))
        {
            return null;
        }

        // The remembered loadout rather than the ship being flown, so a plan for the Cutter in
        // another dock still names its modules (list.md Phase 37 remembered them for exactly this).
        if (state.Loadouts.For(shipId)?.Loadout is not { } loadout)
        {
            return null;
        }

        // The same matching the verdict uses, so the line and the verdict under it cannot come to
        // different conclusions about which module the item is about.
        if (ChecklistEvaluator.Fitted(loadout, subject) is { } module)
        {
            return ChecklistEvaluator.Describe(module);
        }

        // An empty slot still has a name, and it is not `Slot01_Size7`. The layout is keyed on the
        // hull, so this answers for the ship the plan was written for as readily as for the one
        // being flown.
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

    /// <summary>
    /// A ship nothing has named: its hull where the item stored one, and the id either way. The id
    /// stays because a Commander with three Anacondas would otherwise read three identical lines.
    /// </summary>
    private static string Anonymous(string? hull, int shipId)
    {
        var said = $"ship {shipId.ToString(CultureInfo.InvariantCulture)}";

        return HullName(hull) is { Length: > 0 } type ? $"{type} ({said})" : said;
    }

    /// <summary>
    /// The hull as it should be said. Frontier's own spelling arrives from
    /// <c>StoredShips</c> already localised and passes straight through; a symbol goes through the
    /// table, exactly as <see cref="Ships.ShipBuild.HullName"/> does.
    /// </summary>
    private static string? HullName(string? hull) =>
        hull is not { Length: > 0 } ? null : EliteSpecifications.Ship(hull)?.Name ?? hull;

    /// <summary>
    /// The last occurrence of <paramref name="what"/> replaced, which is the one the slot is in:
    /// every plan wording puts the slot at the end, after the blueprint and before the engineer.
    /// Replacing the first would rewrite a blueprint that happened to share the slot's spelling.
    /// </summary>
    private static string Swap(string text, string what, string with)
    {
        var at = text.LastIndexOf(what, StringComparison.OrdinalIgnoreCase);

        return at < 0 ? text : string.Concat(text.AsSpan(0, at), with, text.AsSpan(at + what.Length));
    }
}
