using System.Collections.Immutable;
using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>
/// What one engineer's tally has counted: when counting started, and the units each counted event added,
/// keyed by the event's identity so a journal folded twice counts once.
/// </summary>
public sealed record EngineerTally(DateTimeOffset Start, ImmutableDictionary<string, long> Counted)
{
    public long Total => Counted.Values.Sum();
}

/// <summary>
/// Missions, sales and visits counted for each engineer whose invitation or tribute is a
/// <see cref="UnlockTest.Tally"/>, from the first <c>EngineerProgress</c> that lists them. The journal
/// writes no total for these, so d47 counts them.
/// </summary>
public sealed record EngineerTallies
{
    public static readonly EngineerTallies Empty = new();

    private static readonly Lazy<IReadOnlyList<(int Id, UnlockTest.Tally Test)>> TallyTests = new(() =>
    [
        .. EngineerDirectory.All
            .SelectMany(engineer => new[] { engineer.MeetingTest, engineer.UnlockTest }
                .OfType<UnlockTest.Tally>()
                .Select(test => (engineer.Id, test))),
    ]);

    private ImmutableDictionary<int, EngineerTally> Tallies { get; init; } = ImmutableDictionary<int, EngineerTally>.Empty;

    /// <summary>The star system of the latest <c>Docked</c> or <c>Location</c>.</summary>
    private string? System { get; init; }

    /// <summary>The <c>MarketID</c> of the latest <c>ApproachSettlement</c>.</summary>
    private long? Settlement { get; init; }

    public bool IsKnown => !Tallies.IsEmpty;

    public EngineerTally? For(int engineerId) => Tallies.GetValueOrDefault(engineerId);

    /// <summary>Equal when the counts are, so a dock alone is not a change to them.</summary>
    public bool Equals(EngineerTallies? other) => other is not null && Tallies == other.Tallies;

    public override int GetHashCode() => Tallies.GetHashCode();

    /// <summary>The union of both sets of identities per engineer, from the earlier start; the place is the live one's.</summary>
    internal EngineerTallies With(EngineerTallies live)
    {
        var merged = Tallies;

        foreach (var (id, tally) in live.Tallies)
        {
            merged = merged.SetItem(
                id,
                merged.TryGetValue(id, out var held)
                    ? new EngineerTally(
                        held.Start < tally.Start ? held.Start : tally.Start,
                        held.Counted.SetItems(tally.Counted))
                    : tally);
        }

        return live with { Tallies = merged };
    }

    public EngineerTallies Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "EngineerProgress" => Started(journalEvent),
            "Docked" or "Location" => this with { System = journalEvent.String("StarSystem") ?? System },
            "ApproachSettlement" => this with { Settlement = journalEvent.Long("MarketID") ?? Settlement },
            "MissionCompleted" => Count(journalEvent, UnlockTest.TallyKind.Missions),
            "SellMicroResources" => Count(journalEvent, UnlockTest.TallyKind.Sales),
            "Disembark" => Count(journalEvent, UnlockTest.TallyKind.Visits),
            _ => this,
        };
    }

    private EngineerTallies Started(JournalEvent journalEvent)
    {
        var listed = journalEvent.Items("Engineers").Select(element => element.Int("EngineerID"))
            .Append(journalEvent.Int("EngineerID"))
            .OfType<int>()
            .ToHashSet();

        var tallies = Tallies;

        foreach (var (id, _) in TallyTests.Value)
        {
            if (listed.Contains(id) && !tallies.ContainsKey(id))
            {
                tallies = tallies.SetItem(
                    id, new EngineerTally(journalEvent.Timestamp, ImmutableDictionary<string, long>.Empty));
            }
        }

        return tallies == Tallies ? this : this with { Tallies = tallies };
    }

    private EngineerTallies Count(JournalEvent journalEvent, UnlockTest.TallyKind kind)
    {
        var tallies = Tallies;

        foreach (var (id, test) in TallyTests.Value)
        {
            if (test.Kind != kind
                || !tallies.TryGetValue(id, out var tally)
                || journalEvent.Timestamp < tally.Start
                || Counts(journalEvent, test) is not { } counted)
            {
                continue;
            }

            tallies = tallies.SetItem(id, tally with { Counted = tally.Counted.SetItem(counted.Identity, counted.Units) });
        }

        return tallies == Tallies ? this : this with { Tallies = tallies };
    }

    /// <summary>The identity and units this event counts towards one test, or null when it does not count.</summary>
    private (string Identity, long Units)? Counts(JournalEvent journalEvent, UnlockTest.Tally test)
    {
        switch (test.Kind)
        {
            case UnlockTest.TallyKind.Missions:
                return journalEvent.String("Name") is { } name
                       && journalEvent.Long("MissionID") is { } missionId
                       && At(System, test)
                       && test.Names.Any(stem => name.StartsWith(stem + "_", StringComparison.OrdinalIgnoreCase))
                    ? (missionId.ToString(CultureInfo.InvariantCulture), 1)
                    : null;

            case UnlockTest.TallyKind.Sales:
                if (!At(System, test) || journalEvent.Long("MarketID") is not { } marketId)
                {
                    return null;
                }

                var sold = journalEvent.Items("MicroResources")
                    .Where(item => item.String("Name") is { } symbol
                                   && test.Names.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                    .Sum(item => item.Long("Count") ?? 0);

                return sold > 0
                    ? (string.Create(CultureInfo.InvariantCulture, $"{journalEvent.Timestamp:O}|{marketId}"), sold)
                    : null;

            case UnlockTest.TallyKind.Visits:
                return journalEvent.Bool("OnPlanet")
                       && Settlement is { } settlement
                       && At(journalEvent.String("StarSystem"), test)
                    ? (settlement.ToString(CultureInfo.InvariantCulture), 1)
                    : null;

            default:
                return null;
        }
    }

    private static bool At(string? system, UnlockTest.Tally test) =>
        test.StarSystem is null || string.Equals(system, test.StarSystem, StringComparison.OrdinalIgnoreCase);
}
