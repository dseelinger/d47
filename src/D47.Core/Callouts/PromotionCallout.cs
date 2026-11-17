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

    /// <summary>How long a promotion waits for a higher one on the same ladder; each further promotion restarts it.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(3);

    /// <summary>How long past its time a held promotion is still said; one held while the callout was off is dropped.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    private readonly List<Held> _held = [];

    public string Id => "promotion";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            _held.Clear();
            yield break;
        }

        if (context.State is not { } state)
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

                        var index = _held.FindIndex(h => h.Ladder == ladder);
                        var highest = index >= 0 ? Math.Max(rank, _held[index].Rank) : rank;
                        var held = new Held(ladder, highest, context.Now + Hold);

                        if (index >= 0)
                        {
                            _held[index] = held;
                        }
                        else
                        {
                            _held.Add(held);
                        }
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

        for (var i = 0; i < _held.Count;)
        {
            var held = _held[i];

            if (context.Now < held.Due)
            {
                i++;
                continue;
            }

            _held.RemoveAt(i);

            if (context.Now - held.Due > StaleAfter)
            {
                continue;
            }

            // The folded standing has Percent cleared, so Say says the rank alone.
            var standing = state.Ranks.For(held.Ladder) is { } folded && folded.Rank == held.Rank
                ? folded
                : new RankStanding(held.Ladder, held.Rank);

            yield return new Announcement(
                KeyPrefix + held.Ladder.ToLowerInvariant(),
                $"Promoted. {standing.Say()}.");
        }
    }

    private readonly record struct Held(string Ladder, int Rank, DateTimeOffset Due);
}
