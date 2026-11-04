using System.Globalization;
using D47.Core.Callouts;
using D47.Core.Knowledge;

namespace D47.Core.Watches;

/// <summary>When a watch may speak, and whether a stock watch's condition holds.</summary>
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

    private static DateTimeOffset? Parse(string? stored) =>
        DateTimeOffset.TryParse(
            stored,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
