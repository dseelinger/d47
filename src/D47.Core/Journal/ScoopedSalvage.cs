using System.Collections.Frozen;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Salvage scooped in the current system while it was the pledged Power's own, counted by commodity symbol.</summary>
public sealed record ScoopedSalvage
{
    public static readonly ScoopedSalvage None = new();

    private static readonly Lazy<FrozenDictionary<string, string>> Names = new(() =>
        MaterialCatalogue.All
            .Where(entry => entry.Ledger == MaterialLedger.Cargo && string.Equals(entry.Category, "Salvage", StringComparison.OrdinalIgnoreCase))
            .ToFrozenDictionary(entry => entry.Symbol, entry => entry.Name, StringComparer.OrdinalIgnoreCase));

    private static readonly string[] EscapePods = ["occupiedcryopod", "damagedescapepod"];

    /// <summary>The system the salvage was scooped in.</summary>
    public string? StarSystem { get; init; }

    public IReadOnlyDictionary<string, int> Counts { get; init; } = FrozenDictionary<string, int>.Empty;

    /// <summary>Folds one event; call after the location and the pledge have folded it.</summary>
    public ScoopedSalvage Apply(JournalEvent journalEvent, JournalLocation location, PowerplayPledge pledge)
    {
        switch (journalEvent.Kind)
        {
            case "Died":
                return None;

            case "FSDJump" or "CarrierJump" or "Location"
                when !string.Equals(StarSystem, journalEvent.String("StarSystem"), StringComparison.OrdinalIgnoreCase):
                return None;

            case "CollectCargo"
                when Symbol(journalEvent.String("Type")) is { } scooped
                    && Names.Value.ContainsKey(scooped)
                    && location.StarSystem is { Length: > 0 } system
                    && PowerplayRules.Classify(pledge, location) is MeritSituation.OwnNotUndermined or MeritSituation.OwnUndermined or MeritSituation.OwnUnderminingUnknown:
                return With(scooped, 1, system);

            case "SearchAndRescue" when Symbol(journalEvent.String("Name")) is { } handedIn:
                return With(handedIn, -(journalEvent.Int("Count") ?? 0), StarSystem);

            case "EjectCargo" when Symbol(journalEvent.String("Type")) is { } ejected:
                return With(ejected, -(journalEvent.Int("Count") ?? 0), StarSystem);

            default:
                return this;
        }
    }

    /// <summary>What is still unclaimed other than escape pods, most numerous first, each held down to what the hold carries; the hold is not applied while its manifest is unread.</summary>
    public IReadOnlyList<(string Name, int Count)> Unclaimed(CargoHold hold) =>
        [.. Counts
            .Where(pair => !EscapePods.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .Select(pair => (Name: Names.Value[pair.Key], Count: hold.IsKnown ? Math.Min(pair.Value, hold.Of(pair.Key)) : pair.Value))
            .Where(item => item.Count > 0)
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.Ordinal)];

    private ScoopedSalvage With(string symbol, int change, string? system)
    {
        var current = Counts.GetValueOrDefault(symbol);
        var next = Math.Max(0, current + change);

        if (next == current)
        {
            return this;
        }

        var counts = Counts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (next == 0)
        {
            counts.Remove(symbol);
        }
        else
        {
            counts[symbol] = next;
        }

        return counts.Count == 0
            ? None
            : this with { StarSystem = system, Counts = counts.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase) };
    }

    private static string? Symbol(string? value) => JournalJson.Symbol(value);
}
