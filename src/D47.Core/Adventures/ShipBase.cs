using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>Where a Commander with no fleet carrier keeps their ships: a station holding stored ships, or the Home system setting.</summary>
/// <param name="System">The system's name.</param>
/// <param name="MarketId">The station's market; null where the base is a system.</param>
/// <param name="Station">The station's name, where the journal gave it.</param>
/// <param name="Ships">The stored ships there; 0 for the Home system setting.</param>
public sealed record ShipBase(string System, long? MarketId, string? Station, int Ships)
{
    /// <summary>Whether the base is the Home system setting rather than stored ships.</summary>
    public bool IsHomeSystem => Ships == 0;
}

/// <summary>The rule that a story chapter for a Commander with no fleet carrier ends near their ships.</summary>
public static class ShipBases
{
    /// <summary>The stations tied for the most stored ships, not counting ships in transit or ships with no system.</summary>
    public static IReadOnlyList<ShipBase> Busiest(IEnumerable<StoredShip> ships)
    {
        ArgumentNullException.ThrowIfNull(ships);

        var stations = ships
            .Where(ship => !ship.InTransit && ship.HasSystem)
            .GroupBy(ship => (System: ship.StarSystem.ToUpperInvariant(), ship.MarketId))
            .Select(group => new ShipBase(
                group.First().StarSystem,
                group.Key.MarketId,
                group.Select(ship => ship.StationName).FirstOrDefault(name => name is { Length: > 0 }),
                group.Count()))
            .ToList();

        if (stations.Count == 0)
        {
            return [];
        }

        var most = stations.Max(station => station.Ships);

        return [.. stations.Where(station => station.Ships == most)];
    }

    /// <summary>
    /// The base: none with a fleet carrier; otherwise the station with the most stored ships, a tie going to the one
    /// <paramref name="lightYears"/> puts nearest the Commander; with no stored ship, <paramref name="homeSystem"/>; otherwise none.
    /// </summary>
    public static ShipBase? Of(IEnumerable<StoredShip> ships, bool carrier, string? homeSystem, Func<string, double?>? lightYears = null)
    {
        if (carrier)
        {
            return null;
        }

        var busiest = Busiest(ships);

        if (busiest.Count > 0)
        {
            return busiest
                .OrderBy(station => lightYears?.Invoke(station.System) ?? double.MaxValue)
                .ThenBy(station => station.System, StringComparer.OrdinalIgnoreCase)
                .ThenBy(station => station.MarketId)
                .First();
        }

        return string.IsNullOrWhiteSpace(homeSystem) ? null : new ShipBase(homeSystem.Trim(), null, null, 0);
    }

    /// <summary>Light years from the base within which a chapter's last stop needs no step back: the near-here reach without a carrier.</summary>
    public static double Near(double? jumpRange) => AdventureGenerator.Radius(AdventureReach.NearHere, jumpRange, carrier: false);

    /// <summary>
    /// Whether <paramref name="ask"/> is a story chapter held to the base: not the beacon or finale chapter, not in the black,
    /// not travelling home from the black or to a place the Commander asked for, and not a short story.
    /// </summary>
    public static bool Applies(AdventureAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        return ask is { Story: { Beacon: null, FinaleChapter: null, InTheBlack: false, Home: null, Chosen: null }, Length: not AdventureLength.Short }
               && ask.Rewrite?.Heading is null;
    }
}
