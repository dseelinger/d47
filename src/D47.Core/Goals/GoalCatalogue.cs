namespace D47.Core.Goals;

/// <summary>
/// The arcs that ship (Phase 34, "Goals that outlive a checklist").
/// <para>
/// <b>Nine, and none of them is CQC.</b> Almost nobody plays it, and an arc permanently at nothing
/// is a line of the page spent telling every Commander about a thing they are not doing. That is a
/// judgement about one game mode, and judgements about what somebody cares about are exactly what
/// this page should not be making — so the general form ships beside it and <em>any</em> arc can be
/// set aside. <see cref="Journal.RankState.Careers"/> still reads the CQC rank like every other,
/// because state that is missing a field the journal states is a different kind of wrong.
/// </para>
/// <para>
/// <b>Empire and Federation are not here either</b>, and for a reason rather than by the same
/// judgement: they are navy ranks with no Elite at the top of them, so "Elite in each career" does
/// not describe them and an arc pretending it did would have no definition of done.
/// </para>
/// </summary>
public static class GoalCatalogue
{
    /// <summary>Prefix on every career arc's key, so the store and the page can recognise one.</summary>
    public const string RankPrefix = "rank.";

    public const string Engineers = "engineers";

    public const string Ships = "ships";

    public const string Systems = "exploration.systems";

    public const string Distance = "exploration.distance";

    /// <summary>
    /// The milestone ladders, which is what an arc with no ceiling has instead of a definition of
    /// done: the next rung is the target, and passing the last one is finishing.
    /// </summary>
    public static readonly IReadOnlyList<long> SystemMilestones =
        [100, 500, 1_000, 5_000, 10_000, 25_000, 50_000];

    public static readonly IReadOnlyList<long> DistanceMilestones =
        [5_000, 25_000, 100_000, 500_000, 1_000_000];

    /// <summary>
    /// One career arc per ladder that ends in Elite, in the order the journal writes them.
    /// </summary>
    private static readonly (string Career, string Name, string? Helper)[] Careers =
    [
        ("Combat", "Elite in Combat", null),
        ("Trade", "Elite in Trade", "plot_trade_route"),
        ("Explore", "Elite in Exploration", "plot_exploration_route"),
        ("Soldier", "Elite as a Mercenary", null),
        ("Exobiologist", "Elite in Exobiology", "plot_exobiology_route"),
    ];

    /// <summary>Every built-in arc, in the order the page draws them.</summary>
    public static IReadOnlyList<GoalArc> All =>
    [
        .. Careers.Select(career => new GoalArc
        {
            Key = RankPrefix + career.Career.ToLowerInvariant(),
            Name = career.Name,
            Done = $"{career.Career} rank {Journal.RankStanding.Elite} — Elite.",
            Helper = career.Helper,
        }),

        new GoalArc
        {
            Key = Engineers,
            Name = "Every engineer unlocked",
            Done = "Every engineer in the directory at Unlocked.",
            Unit = "engineers",
            Helper = "find_engineer",
        },

        new GoalArc
        {
            Key = Ships,
            Name = "The ship collection",
            Done = "One of every hull in the shipyard, owned at once.",
            Unit = "hulls",
            Helper = "get_ship_specification",
        },

        new GoalArc
        {
            Key = Systems,
            Name = "Systems visited",
            Done = "The next milestone on the way to fifty thousand.",
            Unit = "systems",
            Helper = "plot_exploration_route",
        },

        new GoalArc
        {
            Key = Distance,
            Name = "Distance flown",
            Done = "The next milestone on the way to a million light years.",
            Unit = "ly",
            Helper = "plot_exploration_route",
        },
    ];

    public static GoalArc? Find(string? key) =>
        key is null
            ? null
            : All.FirstOrDefault(arc => string.Equals(arc.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The journal's career key behind a rank arc, or null if it is not one.</summary>
    public static string? CareerOf(string key) =>
        key.StartsWith(RankPrefix, StringComparison.OrdinalIgnoreCase)
            ? Journal.RankState.Careers.FirstOrDefault(career =>
                string.Equals(RankPrefix + career.ToLowerInvariant(), key, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>The next rung above a figure, or null once the top one is past.</summary>
    public static long? NextMilestone(IReadOnlyList<long> ladder, long have)
    {
        ArgumentNullException.ThrowIfNull(ladder);

        foreach (var rung in ladder)
        {
            if (have < rung)
            {
                return rung;
            }
        }

        return null;
    }
}
