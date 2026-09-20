using D47.Core.Journal;

namespace D47.Core.Goals;

/// <summary>The arcs that ship (Phase 34, "Goals that outlive a checklist").</summary>
public static class GoalCatalogue
{
    /// <summary>Prefix on every career arc's key, so the store and the page can recognise one.</summary>
    public const string RankPrefix = "rank.";

    public const string Engineers = "engineers";

    public const string Ships = "ships";

    public const string Powerplay = "powerplay";

    /// <summary>The rank at which a navy ladder is finished — journal numbering, 0 to 13.</summary>
    public const int NavyTop = 13;

    /// <summary>The rank the Powerplay arc runs to.</summary>
    public const int PowerplayTop = 100;

    /// <summary>One career arc per ladder that ends in Elite, in the order the journal writes them.</summary>
    private static readonly (string Career, string Name, string? Helper)[] Careers =
    [
        ("Combat", "Elite in Combat", null),
        ("Trade", "Elite in Trade", "plot_trade_route"),
        ("Explore", "Elite in Exploration", "plot_exploration_route"),
        ("Soldier", "Elite as a Mercenary", null),
        ("Exobiologist", "Elite in Exobiology", "plot_exobiology_route"),
    ];

    /// <summary>The journal's key for each navy ladder an arc is offered for.</summary>
    public static readonly IReadOnlyList<string> NavyCareers = ["Empire", "Federation"];

    /// <summary>One arc per navy ladder, each running to the top of its own ladder rather than to Elite.</summary>
    private static readonly (string Career, string Name, string Top)[] Navy =
    [
        ("Empire", "Imperial Navy", "King"),
        ("Federation", "Federal Navy", "Admiral"),
    ];

    /// <summary>Every built-in arc, whether or not the Commander's state offers it.</summary>
    public static IReadOnlyList<GoalArc> Every =>
    [
        .. Careers.Select(career => new GoalArc
        {
            Key = RankPrefix + career.Career.ToLowerInvariant(),
            Name = career.Name,
            Done = $"{career.Career} rank {Journal.RankStanding.EliteTop} — Elite V.",
            Helper = career.Helper,
        }),

        .. Navy.Select(navy => new GoalArc
        {
            Key = RankPrefix + navy.Career.ToLowerInvariant(),
            Name = navy.Name,
            Done = $"{navy.Career} rank {NavyTop} — {navy.Top}.",
        }),

        new GoalArc
        {
            Key = Powerplay,
            Name = "Powerplay rank",
            Done = $"Powerplay rank {PowerplayTop}.",
        },

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

    /// <summary>
    /// The arcs this state offers, in the order the page draws them. The Powerplay arc is only one of them
    /// while the Commander is pledged, because rank 0 of 100 for nobody is not a goal they hold.
    /// </summary>
    public static IReadOnlyList<GoalArc> All(CommanderGameState? state) =>
    [
        .. Every.Where(arc =>
            !string.Equals(arc.Key, Powerplay, StringComparison.Ordinal) || state?.Pledge.IsPledged == true),
    ];

    /// <summary>Looks in <see cref="Every"/>, so a key stays reserved in states that do not offer its arc.</summary>
    public static GoalArc? Find(string? key) =>
        key is null
            ? null
            : Every.FirstOrDefault(arc => string.Equals(arc.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The journal's career key behind a rank arc, or null if it is not one.</summary>
    public static string? CareerOf(string key) =>
        key.StartsWith(RankPrefix, StringComparison.OrdinalIgnoreCase)
            ? Journal.RankState.Careers.FirstOrDefault(career =>
                string.Equals(RankPrefix + career.ToLowerInvariant(), key, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>The journal's navy key behind a rank arc, or null if it is not one.</summary>
    public static string? NavyOf(string key) =>
        key.StartsWith(RankPrefix, StringComparison.OrdinalIgnoreCase)
            ? NavyCareers.FirstOrDefault(career =>
                string.Equals(RankPrefix + career.ToLowerInvariant(), key, StringComparison.OrdinalIgnoreCase))
            : null;
}
