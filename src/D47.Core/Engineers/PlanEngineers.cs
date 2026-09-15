using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Engineers;

/// <summary>One engineer a plan names, and how far the Commander is from using them (#195).</summary>
/// <param name="Held">The grade held with them now, or null when they are not unlocked.</param>
/// <param name="LightYears">From the Commander's position, or null when either end is unknown.</param>
public sealed record PlanEngineerEntry(Engineer Engineer, int? Held, double? LightYears);

/// <summary>
/// Who can roll one plan, grouped by whether the Commander can act on them today, nearest first
/// (#195).
/// </summary>
/// <param name="ProgressKnown">
/// Whether <c>EngineerProgress</c> has been read at all — false means <see cref="Unlocked"/> and
/// <see cref="Locked"/> carry nothing and <see cref="All"/> is the whole list, ungrouped.
/// </param>
/// <param name="Grade">The grade the plan asks for, or null for a plan with no grade of its own.</param>
public sealed record PlanEngineerGroups(
    bool ProgressKnown,
    IReadOnlyList<PlanEngineerEntry> All,
    IReadOnlyList<PlanEngineerEntry> Unlocked,
    IReadOnlyList<PlanEngineerEntry> Locked,
    int? Grade)
{
    public static readonly PlanEngineerGroups Empty = new(true, [], [], [], null);

    public bool IsEmpty => All.Count == 0;
}

/// <summary>Groups the engineers a blueprint names into who the Commander can act on today (#195).</summary>
public static class PlanEngineers
{
    /// <summary>
    /// Splits the named engineers into Unlocked and Locked by <see cref="EngineerStanding.IsUnlocked"/>,
    /// each ordered nearest first — or, before <c>EngineerProgress</c> has ever been read, the same
    /// engineers ungrouped and in the same order.
    /// </summary>
    public static PlanEngineerGroups For(
        IEnumerable<string> named, int? grade, EngineerProgressState? progress, StarPosition? from)
    {
        var engineers = named
            .Select(EngineerDirectory.ByName)
            .Where(engineer => engineer is not null)
            .Select(engineer => engineer!)
            .DistinctBy(engineer => engineer.Id)
            .ToList();

        if (engineers.Count == 0)
        {
            return PlanEngineerGroups.Empty;
        }

        var known = progress is { IsKnown: true };

        var all = Sorted(engineers.Select(engineer =>
        {
            var standing = known ? progress!.For(engineer.Id) : null;

            return new PlanEngineerEntry(
                engineer,
                standing is { IsUnlocked: true } ? Math.Max(standing.Rank ?? 1, 1) : null,
                engineer.DistanceFrom(from));
        }));

        if (!known)
        {
            return new PlanEngineerGroups(false, all, [], [], grade);
        }

        var unlocked = all.Where(entry => entry.Held is not null).ToList();
        var locked = all.Where(entry => entry.Held is null).ToList();

        return new PlanEngineerGroups(true, all, unlocked, locked, grade);
    }

    private static IReadOnlyList<PlanEngineerEntry> Sorted(IEnumerable<PlanEngineerEntry> entries) =>
        [
            .. entries
                .OrderBy(entry => entry.LightYears ?? double.MaxValue)
                .ThenBy(entry => entry.Engineer.Name, StringComparer.Ordinal),
        ];
}
