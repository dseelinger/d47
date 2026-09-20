namespace D47.Core.Journal;

/// <summary>Each career's rank names, 0 through 7 — Frontier's own words, not the navy ladders.</summary>
internal static class CareerRankNames
{
    private static readonly Dictionary<string, string[]> Ladders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Combat"] =
            ["Harmless", "Mostly Harmless", "Novice", "Competent", "Expert", "Master", "Dangerous", "Deadly"],
        ["Trade"] =
            ["Penniless", "Mostly Penniless", "Peddler", "Dealer", "Merchant", "Broker", "Entrepreneur", "Tycoon"],
        ["Explore"] =
            ["Aimless", "Mostly Aimless", "Scout", "Surveyor", "Trailblazer", "Pathfinder", "Ranger", "Pioneer"],
        ["Soldier"] =
            ["Defenceless", "Mostly Defenceless", "Rookie", "Soldier", "Gunslinger", "Warrior", "Gladiator", "Deadeye"],
        ["Exobiologist"] =
            ["Directionless", "Mostly Directionless", "Compiler", "Collector", "Cataloguer", "Taxonomist", "Ecologist", "Geneticist"],
        ["CQC"] =
            ["Helpless", "Mostly Helpless", "Amateur", "Semi Professional", "Professional", "Champion", "Hero", "Legend"],
    };

    /// <summary>The rank's name below Elite, or null outside 0 to 7 or an unlisted career.</summary>
    public static string? Name(string career, int rank) =>
        Ladders.TryGetValue(career, out var ladder) && rank >= 0 && rank < ladder.Length
            ? ladder[rank]
            : null;
}
