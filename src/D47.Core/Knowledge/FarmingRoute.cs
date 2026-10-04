using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One trade-down from a site's material: pay <see cref="Give"/> for <see cref="Get"/> into <see cref="ToGrade"/>.</summary>
public sealed record FarmingTradeDown(int ToGrade, int Give, int Get);

/// <summary>One site on the route.</summary>
/// <param name="Kind">Raw, Manufactured or Encoded.</param>
/// <param name="Distance">Light years from the Commander, or null where the position is not known.</param>
/// <param name="IsHere">The Commander is in the site's system.</param>
/// <param name="TradesAcross">The site's material also trades across into any other encoded group.</param>
public sealed record FarmingStop(
    FarmingSite Site,
    string? Kind,
    string Material,
    double? Distance,
    bool IsHere,
    bool TradesAcross,
    IReadOnlyList<FarmingTradeDown> TradeDowns)
{
    public string System => Site.System;

    public string Body => Site.Body;

    public string Coordinates => Site.Coordinates;

    /// <summary>How to collect, such as "shards".</summary>
    public string Method => Site.Method;

    public bool RespawnsOnRelog => Site.RespawnsOnRelog;
}

/// <summary>The farming sites in travel order from the Commander's position.</summary>
/// <param name="PositionKnown">False where the stops are in the table's own order.</param>
public sealed record FarmingRoute(bool PositionKnown, IReadOnlyList<FarmingStop> Stops)
{
    /// <summary>The route, optionally narrowed to one category (Raw, Manufactured or Encoded).</summary>
    public static FarmingRoute For(CommanderGameState? state, string? category = null)
    {
        var sites = FarmingSites.All.Where(site => !site.IsAlternate);

        if (!string.IsNullOrWhiteSpace(category))
        {
            sites = sites.Where(site =>
                string.Equals(FarmingSites.CategoryOf(site), category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var from = state?.Location.StarPos;

        var ordered = from is { } here
            ? sites.OrderBy(site => site.Position.DistanceTo(here)).ToList()
            : sites.ToList();

        return new FarmingRoute(
            from is not null,
            [.. ordered.Select(site => Stop(site, from, state?.Location.StarSystem))]);
    }

    private static FarmingStop Stop(FarmingSite site, StarPosition? from, string? currentSystem)
    {
        var top = MaterialCatalogue.Find(site.MaterialSymbol);
        var trades = new List<FarmingTradeDown>();

        if (top is { Grade: { } topGrade, Line: not null })
        {
            for (var grade = 1; grade < topGrade; grade++)
            {
                if (EngineeringRules.TradeRate(topGrade, grade, sameLine: true) is { } exchange)
                {
                    trades.Add(new FarmingTradeDown(grade, exchange.Paid, exchange.Received));
                }
            }
        }

        return new FarmingStop(
            site,
            FarmingSites.CategoryOf(site),
            top?.Name ?? site.MaterialSymbol,
            from is { } here ? site.Position.DistanceTo(here) : null,
            string.Equals(site.System, currentSystem, StringComparison.OrdinalIgnoreCase),
            string.Equals(site.TopsGroup, "encoded-encryption-files", StringComparison.Ordinal),
            trades);
    }
}
