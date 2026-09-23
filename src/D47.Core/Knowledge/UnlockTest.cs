using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>A reputation band <see cref="EngineerStanding"/> and the journal's own text can carry.</summary>
public enum ReputationBand { Hostile, Unfriendly, Neutral, Cordial, Friendly, Allied }

/// <summary>Where each <see cref="ReputationBand"/> starts and ends on Frontier's −100 to 100 scale.</summary>
public static class ReputationBands
{
    /// <summary>Where a band starts.</summary>
    public static double Floor(ReputationBand band) => band switch
    {
        ReputationBand.Hostile => -100,
        ReputationBand.Unfriendly => -90,
        ReputationBand.Neutral => -35,
        ReputationBand.Cordial => 4,
        ReputationBand.Friendly => 35,
        ReputationBand.Allied => 90,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    /// <summary>Where the next band up starts.</summary>
    public static double Ceiling(ReputationBand band) =>
        band == ReputationBand.Allied ? double.PositiveInfinity : Floor(band + 1);

    /// <summary>The band a reputation value falls in; below −100 reads as Hostile.</summary>
    public static ReputationBand Of(double reputation) =>
        Enum.GetValues<ReputationBand>().LastOrDefault(band => reputation >= Floor(band), ReputationBand.Hostile);
}

/// <summary>
/// One machine-readable test for a meeting or unlock requirement, parsed from the fixed grammar
/// <c>tools/gen-engineers.py</c> writes into <c>Engineers.tsv</c>'s <c>meeting_test</c> and
/// <c>unlock_test</c> columns. Frontier's own prose is never parsed at runtime; the generator has
/// already turned it into one of these.
/// </summary>
public abstract record UnlockTest
{
    /// <summary>A rank the Commander must hold — the journal's <c>Rank</c> event key and a minimum.</summary>
    public sealed record Rank(string Career, int AtLeast) : UnlockTest;

    /// <summary>A reputation band the Commander must be at or past, in either direction.</summary>
    public sealed record Reputation(string Faction, ReputationBand Band, bool AtMost) : UnlockTest;

    /// <summary>A minimum on one figure the journal's <c>Statistics</c> event carries.</summary>
    public sealed record Statistic(string Path, double AtLeast) : UnlockTest;

    /// <summary>
    /// A quantity handed over — <paramref name="Symbol"/> is null where the type alone decides it, as
    /// for a credit-denominated bounty or bond.
    /// </summary>
    public sealed record Contribution(string Type, string? Symbol, long Quantity) : UnlockTest;

    /// <summary>
    /// One cell of <c>meeting_test</c> or <c>unlock_test</c>, read by the grammar the generator writes.
    /// Null for an empty cell or one this grammar does not recognise.
    /// </summary>
    public static UnlockTest? Parse(string? cell)
    {
        if (string.IsNullOrEmpty(cell))
        {
            return null;
        }

        var words = cell.Split(' ');

        return words[0] switch
        {
            "rank" when words.Length == 3
                && int.TryParse(words[2], CultureInfo.InvariantCulture, out var atLeast) =>
                new Rank(words[1], atLeast),

            "reputation" when words.Length >= 4
                && (words[1] == "at-least" || words[1] == "at-most")
                && Enum.TryParse<ReputationBand>(words[2], ignoreCase: true, out var band) =>
                new Reputation(string.Join(' ', words[3..]), band, words[1] == "at-most"),

            "statistic" when words.Length == 3
                && double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var minimum) =>
                new Statistic(words[1], minimum),

            "contribution" when words.Length == 3
                && long.TryParse(words[2], CultureInfo.InvariantCulture, out var quantity) =>
                new Contribution(words[1], null, quantity),

            "contribution" when words.Length == 4
                && long.TryParse(words[3], CultureInfo.InvariantCulture, out var quantity) =>
                new Contribution(words[1], words[2], quantity),

            _ => null,
        };
    }
}
