using D47.Core.Journal;

namespace D47.Core.Knowledge;

public abstract record PlanWrite(long MarketId, string StarSystem, DateTimeOffset At);

/// <summary>What a depot still needs, as an absolute figure per folded symbol.</summary>
public sealed record NeedsWrite(
    long MarketId,
    string StarSystem,
    DateTimeOffset At,
    IReadOnlyDictionary<string, int> Remaining,
    int MaxNeed) : PlanWrite(MarketId, StarSystem, At);

/// <summary>Tonnes the Commander delivered, per folded symbol.</summary>
public sealed record ContributionWrite(
    long MarketId,
    string StarSystem,
    DateTimeOffset At,
    IReadOnlyDictionary<string, int> Delivered) : PlanWrite(MarketId, StarSystem, At);

public sealed record CompleteWrite(long MarketId, string StarSystem, DateTimeOffset At)
    : PlanWrite(MarketId, StarSystem, At);

public static class PlanWrites
{
    /// <summary>The writes one live journal poll's events call for. Pure; never given the priming poll.</summary>
    public static IReadOnlyList<PlanWrite> From(IReadOnlyList<JournalEvent> events, Func<long, string?> systemOf)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(systemOf);

        var writes = new List<PlanWrite>();

        foreach (var journalEvent in events)
        {
            if (journalEvent.Kind is not ("ColonisationConstructionDepot" or "ColonisationContribution")
                || journalEvent.Long("MarketID") is not { } marketId
                || systemOf(marketId) is not { Length: > 0 } system)
            {
                continue;
            }

            if (journalEvent.Kind == "ColonisationContribution")
            {
                var delivered = new Dictionary<string, int>();

                foreach (var row in journalEvent.Items("Contributions"))
                {
                    if (row.Symbol("Name") is { } symbol && row.Int("Amount") is { } amount)
                    {
                        delivered[symbol] = delivered.GetValueOrDefault(symbol) + amount;
                    }
                }

                if (delivered.Count > 0)
                {
                    writes.Add(new ContributionWrite(marketId, system, journalEvent.Timestamp, delivered));
                }

                continue;
            }

            if (journalEvent.Bool("ConstructionComplete"))
            {
                writes.Add(new CompleteWrite(marketId, system, journalEvent.Timestamp));
                continue;
            }

            var remaining = new Dictionary<string, int>();
            var maxNeed = 0;

            foreach (var row in journalEvent.Items("ResourcesRequired"))
            {
                var required = row.Int("RequiredAmount") ?? 0;
                maxNeed += required;

                if (row.Symbol("Name") is { } symbol)
                {
                    remaining[symbol] = Math.Max(0, required - (row.Int("ProvidedAmount") ?? 0));
                }
            }

            writes.Add(new NeedsWrite(marketId, system, journalEvent.Timestamp, remaining, maxNeed));
        }

        return writes;
    }
}
