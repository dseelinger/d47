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

        if (item.Intent is not { } intent)
        {
            return item.Text;
        }

        // **An unlock line says what the invitation asks for**
        // (<a href="https://github.com/dseelinger/d47/issues/22">#22</a>). It used to say only
        // "Unlock Bill Turner at Alioth" and stop, while d47 held the answer all along: the shipped
        // engineer table carries the prose for 34 of the 38, and two other surfaces already read it.
        // The checklist is the one that survives leaving the page, so it is the one that needed it.
        if (intent.Kind == ChecklistIntentKind.EngineerAccess && (intent.Grade ?? 1) <= 1)
        {
            return Invitation(item.Text, intent.Subject);
        }

        if (!ChecklistKeys.SlotShaped(intent.Kind) || item.Scope.Group != ChecklistGroup.Ship)
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
    /// What the invitation asks for, on the end of an unlock line (#22).
    /// <para>
    /// <b>Resolved when the line is drawn, never stored.</b> Same rule that keeps a plan's figures
    /// out of the file: a regenerated table reaches lines already on the list, and a line written
    /// last month does not go on asserting last month's requirement.
    /// </para>
    /// <para>
    /// <b>Four engineers have no invitation text, and they say so rather than nothing.</b> Oden
    /// Geiger, Uma Laszlo, Yarden Bond and Yi Shen have an empty <c>unlock</c> column; what earns
    /// the invitation is filled for all thirty-eight, so there is always something true to say and
    /// it is said as the different thing it is. A line that simply stopped would read as though
    /// nothing were required.
    /// </para>
    /// </summary>
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

        // Short on purpose. What earns the invitation is filled for all thirty-eight and is worth
        // reading, but it runs to a hundred and seventy characters on Oden Geiger — and this line
        // is spoken as well as drawn, with no heading or page around it when it is. So the line
        // says the honest short thing and the drill keeps the prose.
        return found.Meeting is { Length: > 0 }
            ? $"{text} — no invitation task on record"
            : text;
    }

    /// <summary>Lower-cased where it is a whole sentence, so the invitation reads as a clause on the end of one.</summary>
    private static string Sentence(string said) =>
        said.Length > 1 && char.IsUpper(said[0]) && !char.IsUpper(said[1])
            ? char.ToLowerInvariant(said[0]) + said[1..].TrimEnd('.')
            : said.TrimEnd('.');

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
    /// One item's <em>subject</em> as a Commander says it, for a sentence that names slots rather
    /// than draws lines (#154).
    /// <para>
    /// <see cref="Said"/> answers the whole line with the slot swapped inside it; a proposal's
    /// summary needs the slot on its own. Both go through <c>InSlot</c>, so a spoken proposal and
    /// the checklist line it becomes cannot call the same slot two different things — and the raw
    /// journal name is what a Commander was read out loud until this existed: <i>"Armour,
    /// MainEngines, LifeSupport, Radar, Slot05_Size5, Slot06_Size5"</i>.
    /// </para>
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
    /// A slot with no item of its own to describe it — the ones a revision is <em>dropping</em>,
    /// which have no entry in what it proposes. The hull's own layout is the last thing that can
    /// be said about them; null where even that does not resolve, so a caller can tell "I could
    /// not put this into words" from "here are the words" rather than being handed
    /// <c>Slot05_Size5</c> and no way to know (#154).
    /// </summary>
    public static string? Subject(string subject, string? hull) =>
        hull is { Length: > 0 } type ? EliteSpecifications.Slot(type, subject)?.Describe() : null;

    /// <summary>
    /// The module in a slot, as a Commander says it — "7A Shield Generator". Null for a slot
    /// nothing is in, or one on a ship d47 has never been aboard.
    /// </summary>
    /// <summary>
    /// Whether the ship carries more than one of this module, which is what makes the type alone
    /// ambiguous (docs/plans/change-requests.md 44).
    /// <para>
    /// On the item rather than on the readable name: two modules of one item are two of the same
    /// thing however they are spelled, and the name is derived from the item anyway.
    /// </para>
    /// </summary>
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

        // The remembered loadout rather than the ship being flown, so a plan for the Cutter in
        // another dock still names its modules (Phase 37 remembered them for exactly this).
        if (state.Loadouts.For(shipId)?.Loadout is not { } loadout)
        {
            return null;
        }

        // The same matching the verdict uses, so the line and the verdict under it cannot come to
        // different conclusions about which module the item is about.
        if (ChecklistEvaluator.Fitted(loadout, subject) is { } module)
        {
            var described = ChecklistEvaluator.Describe(module);

            // **And the mounting point back where the type alone cannot tell two lines apart**
            // (docs/plans/change-requests.md 44). Naming the type rather than the slot was asked
            // for on 2026-08-24 and was right — `Slot04_Size2` says nothing about what belongs
            // there — but a ship carrying two of a module then draws two identical lines, one done
            // and one open, with nothing on either to say which is which. Reported 2026-08-26
            // against a Kestrel with two 2D hull reinforcements, after an hour spent believing d47
            // had missed an experimental effect it had not.
            //
            // **The condition is the ship's, not the list's**, ruled 2026-08-26: a line reads the
            // same wherever it appears, rather than changing with what happens to be beside it as
            // items are filtered, ordered and ticked.
            if (Twinned(loadout, module)
                && EliteSpecifications.Slot(loadout.Type ?? item.Hull, subject)?.Describe() is { } mount)
            {
                return $"{described} in {mount}";
            }

            return described;
        }

        // Nothing fitted, so the next best answer is what the plan says is going there (asked for
        // 2026-08-24). <b>The module type, never the mounting point.</b> Reported as "Utility Mount
        // 8 and Compartment 4 don't tell me the module type", and d47 knew all along — the ship
        // plan stores the module beside the blueprint and this method had never been shown it.

        if (item.Intent?.Module is { Length: > 0 } planned)
        {
            return planned;
        }

        // And where even the plan does not say — a slot the Commander asked for engineering on
        // without choosing what goes in it — the slot's own name is all there is. It is at least
        // not `Slot01_Size7`: the layout is keyed on the hull, so this answers for the ship the
        // plan was written for as readily as for the one being flown.
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
        hull is not { Length: > 0 } ? null : EliteSpecifications.HullSaid(hull);

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
