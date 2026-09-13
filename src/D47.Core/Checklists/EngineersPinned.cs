using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// Which of the Commander's open items a pinned blueprint could take all the way, wherever they are
/// (#113) — the same question <see cref="EngineersHere"/> asks, with the engineer fixed by the pin
/// rather than by the system the Commander is standing in.
/// </summary>
public static class EngineersPinned
{
    /// <summary>The engineers the Commander has a blueprint pinned with, and their share of the open list.</summary>
    public static IReadOnlyList<EngineerAtHand> For(
        IReadOnlyList<ChecklistItem> items, CommanderGameState? state, IReadOnlyCollection<int> pinnedEngineerIds)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(pinnedEngineerIds);

        if (state is null || pinnedEngineerIds.Count == 0)
        {
            return [];
        }

        var pinned = pinnedEngineerIds
            .Select(EngineerDirectory.ById)
            .OfType<Engineer>()
            .OrderBy(engineer => engineer.Name, StringComparer.Ordinal)
            .ToList();

        return pinned.Count == 0 ? [] : EngineersHere.Assess(pinned, items, state);
    }
}
