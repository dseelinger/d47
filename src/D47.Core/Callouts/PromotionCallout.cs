using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// One remark from the ship's AI on every promotion: the six career ladders, both navies and Powerplay
/// rank (#453).
/// </summary>
public sealed class PromotionCallout : ICallout
{
    /// <summary>Every promotion key starts with this; <see cref="RewordChance"/> always rewords them.</summary>
    public const string KeyPrefix = "promotion.";

    /// <summary>The key for a Powerplay rank.</summary>
    public const string PowerplayKey = KeyPrefix + "powerplay";

    private static readonly IReadOnlyList<string> Ladders = [.. RankState.Careers, "Empire", "Federation"];

    public string Id => "promotion";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "Promotion":
                    foreach (var ladder in Ladders)
                    {
                        if (journalEvent.Int(ladder) is not { } rank)
                        {
                            continue;
                        }

                        // The folded standing has Percent cleared, so Describe says the rank alone.
                        var standing = state.Ranks.For(ladder) is { } folded && folded.Rank == rank
                            ? folded
                            : new RankStanding(ladder, rank);

                        yield return new Announcement(
                            KeyPrefix + ladder.ToLowerInvariant(),
                            $"Promoted. {Careers.Word(ladder)}, {standing.Describe()}.");
                    }

                    break;

                // Rank 0 is written on joining a Power, which is not a promotion.
                case "PowerplayRank" when journalEvent.Int("Rank") is { } powerRank && powerRank > 0
                    && journalEvent.String("Power") is { Length: > 0 } power:
                    yield return new Announcement(
                        PowerplayKey,
                        $"Powerplay rank {powerRank.ToString(CultureInfo.InvariantCulture)} with {power}.");
                    break;
            }
        }
    }
}
