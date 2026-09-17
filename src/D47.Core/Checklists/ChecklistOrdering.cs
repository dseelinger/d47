using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>
/// One project as the ordering sees it: which list it is, its key, and the word a Commander knows it
/// by — "Flamebrand (Anaconda)", "Sol", "custom".
/// </summary>
public sealed record ChecklistProject(ChecklistScope Scope, string Key, string Word);

/// <summary>The checklist in the order the Commander cares about.</summary>
public static class ChecklistOrdering
{
    /// <summary>How many lines are "the next few things" when the list is said out loud.</summary>
    public const int Spoken = 3;

    /// <summary>A project's key — <c>ship:51</c>, <c>system:sol</c>, <c>custom</c>.</summary>
    public static string Key(ChecklistScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return scope.Group == ChecklistGroup.Universal
            ? ChecklistScope.Word(scope.Group)
            : $"{ChecklistScope.Word(scope.Group)}:{(scope.Key ?? string.Empty).Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// Whether a project is where the Commander is right now: the ship being flown, the system being
    /// stood in, the suit being worn, the weapon in hand.
    /// </summary>
    public static bool IsHere(ChecklistScope scope, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (state is null)
        {
            return false;
        }

        return scope.Group switch
        {
            ChecklistGroup.Ship => ChecklistEvaluator.IsActive(scope, state.Ship),

            ChecklistGroup.System => state.Location.StarSystem is { Length: > 0 } here
                && string.Equals(scope.Key, here, StringComparison.OrdinalIgnoreCase),

            ChecklistGroup.Suit => state.OnFoot.SuitId is { } suit
                && string.Equals(
                    scope.Key, suit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),

            ChecklistGroup.Weapon => state.OnFoot.Weapons.Any(carried =>
                carried.ModuleId is { } id
                && string.Equals(
                    scope.Key, id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)),

            _ => false,
        };
    }

    /// <summary>
    /// Every project with a live item, in the order the list reads: here-and-now first, then as they
    /// first appear in the file.
    /// </summary>
    public static IReadOnlyList<ChecklistProject> Projects(
        ChecklistDocument document, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(document);

        var seen = new List<ChecklistProject>();

        foreach (var item in document.Items)
        {
            var key = Key(item.Scope);

            if (seen.Any(project => string.Equals(project.Key, key, StringComparison.Ordinal)))
            {
                continue;
            }

            seen.Add(new ChecklistProject(
                item.Scope, key, ChecklistWording.Where(item.Scope, item.Hull, state)));
        }

        return
        [
            .. seen
                .Select((project, appeared) => (project, appeared))
                .OrderBy(entry => IsHere(entry.project.Scope, state) ? 0 : 1)
                .ThenBy(entry => entry.appeared)
                .Select(entry => entry.project),
        ];
    }

    /// <summary>
    /// Every live item, in the order the Commander cares about: their projects in <see
    /// cref="Projects"/>' order, and within one the actionable lines above the blocked — with the
    /// stored order as the tiebreak throughout, which is what keeps the item movers meaning something.
    /// </summary>
    public static IReadOnlyList<ChecklistItem> Arrange(
        ChecklistDocument document, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(document);

        var at = Projects(document, state)
            .Select((project, position) => (project.Key, position))
            .ToDictionary(entry => entry.Key, entry => entry.position, StringComparer.Ordinal);

        var live = document.Items.ToList();

        // Where each line sits in the file, which used to be implicit in OrderBy's stability and has to be
        // explicit now that an effect can borrow its upgrade's place.
        var stored = live
            .Select((item, index) => (item, index))
            .ToDictionary(entry => entry.item, entry => entry.index);

        // An experimental effect sorts as though it were its upgrade (GitHub issue 31).
        var upgrade = live.ToDictionary(
            item => item,
            item => ChecklistKinship.UpgradeFor(item, live));

        return
        [
            .. live
                .OrderBy(item => at.GetValueOrDefault(Key(item.Scope)))
                .ThenBy(item => Band(upgrade[item] ?? item))
                .ThenBy(item => stored[upgrade[item] ?? item])

                // And the effect after its upgrade rather than before it, which is the order they are born in
                // and the order the work happens in.
                .ThenBy(item => upgrade[item] is null ? 0 : 1),
        ];
    }

    /// <summary>
    /// Where a line sorts within its project, by the one question that matters at the top of a list:
    /// can anything be done about it?
    /// </summary>
    private static int Band(ChecklistItem item) => item.State switch
    {
        ChecklistState.Open => 0,
        ChecklistState.Elsewhere => 1,
        ChecklistState.Unverified => 2,
        ChecklistState.Blocked => 3,
        ChecklistState.Stale => 4,
        _ => 5,
    };
}
