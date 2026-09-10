using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// An engineer whose system the Commander is standing in, and which of their open checklist items that
/// engineer can actually work on.
/// </summary>
/// <param name="Engineer">Who, and where their base is.</param>
/// <param name="Unlocked">Whether the Commander has them at all.</param>
/// <param name="Rank">The Commander's grade with them, where the journal has said.</param>
/// <param name="Ready">Open items this engineer could roll today.</param>
/// <param name="OutOfRank">
/// Open items they offer the blueprint for but cannot roll to the grade wanted yet.
/// </param>
/// <param name="Partial">
/// Open items they offer the blueprint for but only below the grade wanted — work that can be started
/// here and has to be finished elsewhere (change-requests.md 35).
/// </param>
public sealed record EngineerAtHand(
    Engineer Engineer,
    bool Unlocked,
    int? Rank,
    IReadOnlyList<ChecklistItem> Ready,
    IReadOnlyList<ChecklistItem> OutOfRank,
    IReadOnlyList<PartialGrade> Partial)
{
    /// <summary>Whether there is anything worth saying about this engineer at all.</summary>
    public bool HasWork => Ready.Count > 0 || OutOfRank.Count > 0;

    /// <summary>
    /// Whether this engineer is worth listing at all, which is a wider question than whether they are
    /// worth announcing.
    /// </summary>
    public bool IsWorthListing => HasWork || Partial.Count > 0;

    /// <summary>One sentence, for the opening callout and for a spoken answer.</summary>
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
            return $"{where} — nothing on your list they can craft yet, "
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
/// A line this engineer can advance but not finish — asked for 2026-08-23 as "Include Partial Grades".
/// </summary>
/// <param name="Item">The line itself.</param>
/// <param name="Reaches">The grade this engineer can take it to.</param>
/// <param name="Wanted">The grade the line asks for, which somebody else has to reach.</param>
/// <param name="RidesAlong">
/// True where this line has no grade of its own and is here because the module's blueprint is — an
/// experimental effect, which is part of finishing that module rather than an errand on its own
/// (reported 2026-08-24).
/// </param>
public sealed record PartialGrade(ChecklistItem Item, int Reaches, int Wanted, bool RidesAlong = false)
{
    /// <summary>
    /// How far it goes, said on the line itself so the answer is on screen and not only in the help —
    /// "Lei Cheung takes this to 3 of 5".
    /// </summary>
    public string Describe(string engineer)
    {
        var reaches = Reaches.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        var wanted = Wanted.ToString(global::System.Globalization.CultureInfo.InvariantCulture);

        // An effect is applied outright at any grade, so saying it goes "to 3 of 5" would be a sentence about
        // the module wearing the line's clothes.
        return RidesAlong
            ? $"{engineer} can apply this, but only takes that module to {reaches} of {wanted}"
            : $"{engineer} takes this to {reaches} of {wanted}";
    }
}

/// <summary>
/// Which of the Commander's open items the engineer they are standing next to could work on (asked for
/// 2026-08-20).
/// </summary>
public static class EngineersHere
{
    /// <summary>The engineers in the Commander's current system, with their share of the open list.</summary>
    public static IReadOnlyList<EngineerAtHand> For(
        IReadOnlyList<ChecklistItem> items, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (state?.Location.StarSystem is not { Length: > 0 } system)
        {
            return [];
        }

        var here = EngineerDirectory.InSystem(system)
            .OrderBy(engineer => engineer.Name, StringComparer.Ordinal)
            .ToList();

        if (here.Count == 0)
        {
            return [];
        }

        // Open, derived, and about engineering.
        var open = items
            .Where(item => item.IsLive && !item.IsComplete)
            .Where(item => item.Intent?.Kind is ChecklistIntentKind.Blueprint or ChecklistIntentKind.Experimental)

            // And there has to be something in the slot to roll (GitHub issue 41).
            .Where(item => IsFitted(item, state))
            .ToList();

        return
        [
            .. here.Select(engineer =>
            {
                var standing = state.Engineers.Standings
                    .FirstOrDefault(known => known.Id == engineer.Id);

                var ready = new List<ChecklistItem>();
                var waiting = new List<ChecklistItem>();
                var partial = new List<PartialGrade>();

                foreach (var item in open)
                {
                    if (Ceiling(engineer, item, state) is not { } ceiling)
                    {
                        continue;
                    }

                    // The third band (change-requests.md 35).
                    if (item.Intent is { Grade: { } wanted } && ceiling < wanted)
                    {
                        partial.Add(new PartialGrade(item, ceiling, wanted));
                        continue;
                    }

                    // Rank only gates a graded blueprint.
                    var gated = item.Intent is { Kind: ChecklistIntentKind.Blueprint, Grade: { } grade }
                                && (standing?.Rank is not { } rank
                                    || EngineeringRules.RollsFor(grade, rank) is null);

                    (gated ? waiting : ready).Add(item);
                }

                // An experimental effect goes where its module's blueprint went, reported 2026-08-24
                // against the band above: "if I'm not showing partial grades, then don't show that
                // module's corresponding experimental effect".
                foreach (var effect in ready
                             .Where(item => item.Intent?.Kind == ChecklistIntentKind.Experimental)
                             .ToList())
                {
                    // Through ChecklistKinship, so this and the ordering answer "is this that module's
                    // effect?" the same way rather than deriving it twice (GitHub issue 31).
                    var sibling = partial.FirstOrDefault(part =>
                        ChecklistKinship.SameModule(part.Item, effect));

                    if (sibling is null)
                    {
                        continue;
                    }

                    ready.Remove(effect);
                    partial.Add(new PartialGrade(effect, sibling.Reaches, sibling.Wanted, RidesAlong: true));
                }

                return new EngineerAtHand(
                    engineer,
                    standing?.IsUnlocked ?? false,
                    standing?.Rank,
                    ready,
                    waiting,
                    partial);
            }).Where(found => found.IsWorthListing)
        ];
    }

    /// <summary>Whether this engineer offers what the item asks for.</summary>
    private static int? Ceiling(Engineer engineer, ChecklistItem item, CommanderGameState state)
    {
        if (item.Intent is not { } intent)
        {
            return null;
        }

        var wanted = intent.Kind == ChecklistIntentKind.Experimental
            ? BlueprintKind.Experimental
            : BlueprintKind.Modification;

        // Narrowed to the module actually in the slot where d47 can see it, because a blueprint name belongs
        // to several module kinds and they do not share an engineer list — Heavy Duty on a Shield Booster is
        // Lei Cheung's and on a Hull Reinforcement Package it is not.
        return BlueprintCatalogue.Named(intent.Detail ?? intent.Subject, ModuleOf(item, state))
            .Where(recipe => recipe.Kind == wanted)

            // At the grade the line actually asks for, reported 2026-08-23.
            .Where(recipe => EngineerDirectory.IsNamedIn(recipe.Engineers, engineer))
            .Select(recipe => recipe.Grade ?? Ungraded)
            .DefaultIfEmpty(NotTheirs)
            .Max() is var top && top == NotTheirs
            ? null
            : top;
    }

    /// <summary>
    /// An experimental has no grade and is bought outright, so its ceiling is a number that clears
    /// every comparison rather than a real grade.
    /// </summary>
    private const int Ungraded = int.MaxValue;

    /// <summary>Not this engineer's blueprint at all, which is a different answer from grade 0.</summary>
    private const int NotTheirs = -1;

    /// <summary>
    /// What is fitted in the slot this item is about, or null where d47 has never seen the ship.
    /// </summary>
    private static ModuleSpecification? ModuleOf(ChecklistItem item, CommanderGameState state)
    {
        if (Fitted(item, state) is not { } fitted)
        {
            return null;
        }

        // The specification rather than its name: see the comment in Offers above.
        return EliteSpecifications.Module(fitted.Item);
    }

    /// <summary>The module in this item's slot, or null where the slot is empty or the ship unseen.</summary>
    private static ShipModule? Fitted(ChecklistItem item, CommanderGameState state)
    {
        if (item.Intent?.Subject is not { Length: > 0 } slot)
        {
            return null;
        }

        return LoadoutFor(item, state) is not { } loadout
            ? null
            : loadout.Modules.FirstOrDefault(module =>
                string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether there is anything in this item's slot for an engineer to work on (GitHub issue 41).
    /// </summary>
    private static bool IsFitted(ChecklistItem item, CommanderGameState state) =>
        LoadoutFor(item, state) is null || Fitted(item, state) is not null;

    /// <summary>
    /// Which loadout this item's modules are read from: the live one for the ship being flown, and for
    /// a line that is not about a ship in particular because there is nothing better to offer it; the
    /// remembered one for any other ship.
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
