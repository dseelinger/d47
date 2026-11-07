using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The systems the galaxy map draws as unvisited.</summary>
internal static class UnvisitedStars
{
    /// <summary>The systems in <paramref name="nearby"/> with a position that no step in <paramref name="steps"/> reached, by name or address.</summary>
    public static List<GalaxyMapStar> Among(IReadOnlyList<SystemSummary> nearby, IReadOnlyList<TravelStep> steps)
    {
        var addresses = steps.Select(step => step.SystemAddress).ToHashSet();
        var names = steps.Select(step => step.System).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. nearby
            .Where(system => system.Position is not null
                && !names.Contains(system.Name)
                && (system.SystemAddress is not { } address || !addresses.Contains(address)))
            .Select(system => new GalaxyMapStar(system.Name, system.Position!.Value))];
    }
}
