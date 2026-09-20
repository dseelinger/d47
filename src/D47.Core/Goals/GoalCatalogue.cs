namespace D47.Core.Goals;

/// <summary>The arcs that ship (Phase 34, "Goals that outlive a checklist").</summary>
public static class GoalCatalogue
{
    /// <summary>Prefix on every career arc's key, so the store and the page can recognise one.</summary>
    public const string RankPrefix = "rank.";

    public const string Engineers = "engineers";

    public const string Ships = "ships";

    /// <summary>One career arc per ladder that ends in Elite, in the order the journal writes them.</summary>
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
            Done = $"{career.Career} rank {Journal.RankStanding.EliteTop} — Elite V.",
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
}
