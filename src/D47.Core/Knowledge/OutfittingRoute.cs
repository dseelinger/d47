using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One station on an outfitting route and the items to buy there.</summary>
/// <param name="Distance">Light years from the previous stop; from the reference system for the first.</param>
public sealed record OutfittingStop(string System, string Station, double? Distance, IReadOnlyList<string> Items)
{
    public StarPosition? Position { get; init; }
}

/// <summary>The stations that between them sell what a Commander asked for.</summary>
public sealed record OutfittingRoute(IReadOnlyList<OutfittingStop> Stops, IReadOnlyList<string> NotFound)
{
    /// <summary>The system the route was plotted out from.</summary>
    public string? Reference { get; init; }

    public double TotalDistance => Stops.Sum(stop => stop.Distance ?? 0);
}

/// <summary>Picks the stations an outfitting route visits. No I/O.</summary>
public static class OutfittingRoutePlanner
{
    /// <summary>The most stops one route has.</summary>
    public const int MostStops = 6;

    /// <summary>
    /// Covers <paramref name="items"/> greedily, the station selling most of what is left first and the nearer
    /// one on a tie, then orders the chosen stations nearest-next from the reference system.
    /// </summary>
    public static OutfittingRoute Plan(
        IReadOnlyList<OutfittingItem> items,
        IReadOnlyList<OutfitterStation> stations,
        string? reference,
        StarPosition? referencePosition = null)
    {
        var uncovered = items.Distinct().ToList();
        var candidates = stations
            .GroupBy(station => station.Station.MarketId is { } market
                ? market.ToString()
                : $"{station.Station.SystemName}/{station.Station.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var chosen = new List<(OutfitterStation Station, List<OutfittingItem> Buys)>();

        while (uncovered.Count > 0 && chosen.Count < MostStops)
        {
            var best = candidates
                .Select(station => (Station: station, Buys: station.Sells.Where(uncovered.Contains).ToList()))
                .Where(entry => entry.Buys.Count > 0)
                .OrderByDescending(entry => entry.Buys.Count)
                .ThenBy(entry => entry.Station.Station.Distance ?? double.MaxValue)
                .Cast<(OutfitterStation Station, List<OutfittingItem> Buys)?>()
                .FirstOrDefault();

            if (best is not { } pick)
            {
                break;
            }

            chosen.Add(pick);
            candidates.Remove(pick.Station);
            uncovered.RemoveAll(pick.Buys.Contains);
        }

        var ordered = new List<OutfittingStop>();
        var here = referencePosition;

        while (chosen.Count > 0)
        {
            var next = chosen
                .OrderBy(entry => Leg(here, entry.Station.Station, ordered.Count == 0) ?? double.MaxValue)
                .ThenBy(entry => entry.Station.Station.Distance ?? double.MaxValue)
                .First();

            chosen.Remove(next);

            var summary = next.Station.Station;

            ordered.Add(new OutfittingStop(
                summary.SystemName,
                summary.Name,
                Leg(here, summary, ordered.Count == 0),
                [.. next.Buys.Select(item => item.Display)])
            {
                Position = summary.Position,
            });

            here = summary.Position ?? here;
        }

        return new OutfittingRoute(ordered, [.. uncovered.Select(item => item.Display)]) { Reference = reference };
    }

    /// <summary>Light years to a station from the last stop, or from the reference system for the first stop.</summary>
    private static double? Leg(StarPosition? from, StationSummary station, bool first)
    {
        if (first && station.Distance is { } fromReference)
        {
            return fromReference;
        }

        return from is { } start && station.Position is { } position ? start.DistanceTo(position) : station.Distance;
    }
}
