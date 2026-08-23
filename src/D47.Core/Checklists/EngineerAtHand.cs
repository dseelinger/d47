using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// An engineer whose system the Commander is standing in, and which of their open checklist items
/// that engineer can actually work on.
/// </summary>
/// <param name="Engineer">Who, and where their base is.</param>
/// <param name="Unlocked">Whether the Commander has them at all.</param>
/// <param name="Rank">
/// The Commander's grade with them, where the journal has said. Null for an engineer they have
/// never met — which is a different answer from grade 0 and is why this is nullable.
/// </param>
/// <param name="Ready">Open items this engineer could roll today.</param>
/// <param name="OutOfRank">
/// Open items they offer the blueprint for but cannot roll to the grade wanted yet. Kept apart
/// from <paramref name="Ready"/> because it is a different errand: one is work, the other is a
/// reason to go and do some of their other work first.
/// </param>
public sealed record EngineerAtHand(
    Engineer Engineer,
    bool Unlocked,
    int? Rank,
    IReadOnlyList<ChecklistItem> Ready,
    IReadOnlyList<ChecklistItem> OutOfRank)
{
    /// <summary>Whether there is anything worth saying about this engineer at all.</summary>
    public bool HasWork => Ready.Count > 0 || OutOfRank.Count > 0;

    /// <summary>
    /// One sentence, for the opening callout and for a spoken answer.
    /// <para>
    /// <b>What they can do today leads</b>, because that is the errand. The out-of-rank count
    /// follows only when there is one, and an engineer the Commander has not unlocked says so
    /// instead of counting work they cannot start.
    /// </para>
    /// </summary>
    public string Describe()
    {
        var where = Engineer.Station is { Length: > 0 } station
            ? $"{Engineer.Name} is here, at {station}"
            : $"{Engineer.Name} is here";

        if (!Unlocked)
        {
            return $"{where} — {Count(Ready.Count + OutOfRank.Count)} on your list, and you have "
                   + "not unlocked them.";
        }

        if (Ready.Count == 0)
        {
            return $"{where} — nothing on your list they can roll yet, "
                   + $"{Count(OutOfRank.Count)} waiting on your grade with them.";
        }

        return OutOfRank.Count > 0
            ? $"{where}, and can do {Count(Ready.Count)} on your list. "
              + $"{Count(OutOfRank.Count)} more waiting on your grade with them."
            : $"{where}, and can do {Count(Ready.Count)} on your list.";
    }

    private static string Count(int many) =>
        many == 1 ? "one item" : $"{many.ToString(global::System.Globalization.CultureInfo.InvariantCulture)} items";
}

/// <summary>
/// Which of the Commander's open items the engineer they are standing next to could work on
/// (asked for 2026-08-20).
/// <para>
/// <b>The join nothing was making.</b> Every input has been on disk for phases — the recipe rows
/// name their engineers, the directory knows where each engineer is, <c>EngineerProgress</c> knows
/// who is unlocked and at what grade, and the journal knows the system — and no reader put them
/// together. So "I am in Laksak, what can I retire here?" returned the whole list, and the opening
/// callout offered an unlock hint about somebody else while the Commander stood in Lei Cheung's
/// system with thirty items he could roll.
/// </para>
/// <para>
/// <b>Rank is a gate and not a filter.</b> An item this engineer offers but cannot yet roll to the
/// grade wanted is still information — it is the reason to do some of their other work first — so
/// it is reported beside the ready ones rather than dropped.
/// </para>
/// </summary>
public static class EngineersHere
{
    /// <summary>
    /// The engineers in the Commander's current system, with their share of the open list. Empty
    /// where the Commander is nowhere d47 knows, or where no engineer is based there.
    /// </summary>
    public static IReadOnlyList<EngineerAtHand> For(
        IReadOnlyList<ChecklistItem> items, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (state?.Location.StarSystem is not { Length: > 0 } system)
        {
            return [];
        }

        var here = EngineerDirectory.All
            .Where(engineer => string.Equals(engineer.System, system, StringComparison.OrdinalIgnoreCase))
            .OrderBy(engineer => engineer.Name, StringComparer.Ordinal)
            .ToList();

        if (here.Count == 0)
        {
            return [];
        }

        // Open, derived, and about engineering. An authored line is nobody's to roll, and a
        // commodity a construction site wants is not an engineer's errand either.
        var open = items
            .Where(item => item.IsLive && !item.IsComplete)
            .Where(item => item.Intent?.Kind is ChecklistIntentKind.Blueprint or ChecklistIntentKind.Experimental)
            .ToList();

        return
        [
            .. here.Select(engineer =>
            {
                var standing = state.Engineers.Standings
                    .FirstOrDefault(known => known.Id == engineer.Id);

                var ready = new List<ChecklistItem>();
                var waiting = new List<ChecklistItem>();

                foreach (var item in open)
                {
                    if (!Offers(engineer, item, state))
                    {
                        continue;
                    }

                    // Rank only gates a graded blueprint. An experimental is bought outright, so
                    // an engineer who offers it can apply it whatever the Commander's grade.
                    var gated = item.Intent is { Kind: ChecklistIntentKind.Blueprint, Grade: { } grade }
                                && (standing?.Rank is not { } rank
                                    || EngineeringRules.RollsFor(grade, rank) is null);

                    (gated ? waiting : ready).Add(item);
                }

                return new EngineerAtHand(
                    engineer,
                    standing?.IsUnlocked ?? false,
                    standing?.Rank,
                    ready,
                    waiting);
            }).Where(found => found.HasWork)
        ];
    }

    /// <summary>
    /// Whether this engineer offers what the item asks for.
    /// <para>
    /// Matched through the recipes for the item's own module, so a blueprint name shared by eight
    /// module kinds resolves to the one the item is actually about — the same reason
    /// <c>BlueprintCatalogue.ForModule</c> exists.
    /// </para>
    /// </summary>
    private static bool Offers(Engineer engineer, ChecklistItem item, CommanderGameState state)
    {
        if (item.Intent is not { } intent)
        {
            return false;
        }

        var wanted = intent.Kind == ChecklistIntentKind.Experimental
            ? BlueprintKind.Experimental
            : BlueprintKind.Modification;

        // Narrowed to the module actually in the slot where d47 can see it, because a blueprint
        // name belongs to several module kinds and they do not share an engineer list — Heavy Duty
        // on a Shield Booster is Lei Cheung's and on a Hull Reinforcement Package it is not.
        // Where the module cannot be seen the name alone is used, which is looser and is still
        // better than saying nothing about a ship d47 has never been aboard.
        //
        // By the specification rather than by its name, reported 2026-08-23: the name is Frontier's
        // product name and the recipe table speaks categories, so "Bi-Weave Shield Generator" met
        // no row called "Shield Generator" and the line left every engineer's answer in silence.
        return BlueprintCatalogue.Named(intent.Detail ?? intent.Subject, ModuleOf(item, state))
            .Where(recipe => recipe.Kind == wanted)
            .Any(recipe => recipe.Engineers.Contains(engineer.Name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What is fitted in the slot this item is about, or null where d47 has never seen the ship.
    /// <para>
    /// <b>The item's own ship, not the one being flown</b> (change-requests.md 33). The comment
    /// this replaces said an item about a ship in another dock "resolves to null here by design",
    /// and it did not: slot names are shared across hulls — every ship has a
    /// <c>TinyHardpoint5</c> — so the live loadout answered with <em>this</em> ship's module and
    /// the blueprint match was then narrowed to it. A Heavy Duty roll wanted on the Krait's shield
    /// booster was measured against the Anaconda's chaff launcher and dropped out of the answer
    /// altogether, which is the opposite of the loosening the comment claimed.
    /// </para>
    /// <para>
    /// The remembered loadout is the same source <see cref="ChecklistWording"/> reads to name the
    /// module on the line, and for the same reason — list.md Phase 37 remembered them for exactly
    /// this. Null survives only for a ship d47 has never been aboard, which is the case the
    /// name-alone match above was always for.
    /// </para>
    /// </summary>
    private static ModuleSpecification? ModuleOf(ChecklistItem item, CommanderGameState state)
    {
        if (item.Intent?.Subject is not { Length: > 0 } slot)
        {
            return null;
        }

        if (LoadoutFor(item, state) is not { } loadout)
        {
            return null;
        }

        var fitted = loadout.Modules.FirstOrDefault(module =>
            string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase));

        // The specification rather than its name: see the comment in Offers above. The name is a
        // product name and the recipe table is keyed on the module's type.
        return fitted is null ? null : EliteSpecifications.Module(fitted.Item);
    }

    /// <summary>
    /// Which loadout this item's modules are read from: the live one for the ship being flown, and
    /// for a line that is not about a ship in particular because there is nothing better to offer
    /// it; the remembered one for any other ship. Null where that ship has never been seen.
    /// </summary>
    private static ShipLoadout? LoadoutFor(ChecklistItem item, CommanderGameState state)
    {
        if (item.Scope.Group != ChecklistGroup.Ship)
        {
            return state.Ship;
        }

        return ChecklistEvaluator.IsActive(item.Scope, state.Ship)
            ? state.Ship
            : int.TryParse(
                item.Scope.Key,
                global::System.Globalization.NumberStyles.Integer,
                global::System.Globalization.CultureInfo.InvariantCulture,
                out var shipId)
                ? state.Loadouts.For(shipId)?.Loadout
                : null;
    }
}
