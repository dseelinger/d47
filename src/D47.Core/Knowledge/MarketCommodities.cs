namespace D47.Core.Knowledge;

/// <summary>Every commodity name a market trades, ordinary and rare.</summary>
public static class MarketCommodities
{
    public static IReadOnlyList<string> Names { get; } =
        [.. MaterialCatalogue.All
            .Where(entry => entry.Ledger is MaterialLedger.Cargo or MaterialLedger.RareCargo)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
}
