using System.Globalization;
using D47.Core.Callouts;
using D47.Core.Knowledge;

namespace D47.Core.Watches;

/// <summary>When a watch may speak, whether a stock watch's condition holds, and which station a next-system watch fires for.</summary>
public static class WatchRule
{
    /// <summary>True when a BGS tick is known and the watch has not already fired for it.</summary>
    public static bool MayFire(DateTimeOffset? tick, string? firedForTick)
    {
        if (tick is not { } current)
        {
            return false;
        }

        return !(Parse(firedForTick) is { } firedFor && current - firedFor <= BgsTickCallout.SameTickWithin);
    }

    /// <summary>True when a dated quote no older than the price-age limit is past the watch's threshold.</summary>
    public static bool StockHolds(Watch watch, StationQuote? quote, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(watch);

        if (watch.Kind != WatchKind.Stock || watch.Threshold is not { } threshold || quote?.UpdatedAt is not { } updated)
        {
            return false;
        }

        if (now - updated > TimeSpan.FromHours(CommunityGoalSearch.MaxPriceAgeHours))
        {
            return false;
        }

        return watch.Comparison switch
        {
            StockComparison.Above => quote.Stock > threshold,
            StockComparison.Below => quote.Stock < threshold,
            _ => false,
        };
    }

    /// <summary>
    /// The fixed station in <paramref name="system"/> with the watch's service, kind and pad, nearest the entry point;
    /// null when none has them or the watch is not a next-system watch.
    /// </summary>
    public static StationSummary? Arrival(Watch watch, string system, IReadOnlyList<StationSummary> stations)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(stations);

        if (watch.Kind != WatchKind.NextSystem || NextSystemWatch.ServiceFor(watch.Service) is not { } service)
        {
            return null;
        }

        var kind = NextSystemWatch.KindFor(service.Name, watch.ServiceKind);

        return stations
            .Where(station => string.Equals(station.SystemName, system.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(station => station.IsFixed)
            .Where(station => station.Services.Any(offered => string.Equals(offered, service.Field, StringComparison.OrdinalIgnoreCase)))
            .Where(station => kind is null || string.Equals(KindAt(station, service.Name), kind, StringComparison.OrdinalIgnoreCase))
            .Where(station => watch.MinPad is not { } pad || station.Admits(pad))
            .OrderBy(station => station.DistanceToArrival ?? double.MaxValue)
            .FirstOrDefault();
    }

    private static string? KindAt(StationSummary station, string service) => service switch
    {
        NextSystemWatch.MaterialTrader => station.TraderType,
        NextSystemWatch.TechnologyBroker => station.TechnologyBroker,
        _ => null,
    };

    private static DateTimeOffset? Parse(string? stored) =>
        DateTimeOffset.TryParse(
            stored,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
