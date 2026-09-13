namespace D47.Core.Knowledge;

/// <summary>The Empire and Federation rank ladders, 0 to 14, as coriolis-data's requirements gate ships by.</summary>
public static class NavalRanks
{
    private static readonly string[] Empire =
    [
        "None", "Outsider", "Serf", "Master", "Squire", "Knight", "Lord", "Baron",
        "Viscount", "Count", "Earl", "Marquis", "Duke", "Prince", "King",
    ];

    private static readonly string[] Federation =
    [
        "None", "Recruit", "Cadet", "Midshipman", "Petty Officer", "Chief Petty Officer",
        "Warrant Officer", "Ensign", "Lieutenant", "Lieutenant Commander", "Post Commander",
        "Post Captain", "Rear Admiral", "Vice Admiral", "Admiral",
    ];

    /// <summary>The Empire rank name at this grade, or null outside 0 to 14.</summary>
    public static string? EmpireName(int rank) => Named(Empire, rank);

    /// <summary>The Federation rank name at this grade, or null outside 0 to 14.</summary>
    public static string? FederationName(int rank) => Named(Federation, rank);

    private static string? Named(string[] ladder, int rank) =>
        rank >= 0 && rank < ladder.Length ? ladder[rank] : null;
}
