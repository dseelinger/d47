using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One rare good as listed: its station, distance, and what the rare index knows of it.</summary>
public sealed record RareListingRow(RareEntry Rare, RareIndexRow? Index, double Distance, int? FarPrice)
{
    public int? Supply => Index?.Stock;

    public int? Price => Index?.BuyPrice;

    public int? Profit => FarPrice is { } far && Price is { } price ? far - price : null;

    public long? Total => Supply is { } supply && FarPrice is { } far ? (long)supply * far : null;

    public DateTimeOffset? ReportedAt => Index?.ReportedAt;
}

/// <summary>The answer to <c>list_rares</c>: every rare in reach, in the order asked.</summary>
/// <param name="Rows">Every rare within reach, not one page.</param>
/// <param name="NoIndex">Whether the rare index could not be fetched, so supply and prices are not known.</param>
public sealed record RareSearchResult(IReadOnlyList<RareListingRow> Rows, bool NoIndex)
{
    public int Total => Rows.Count;
}

/// <summary>The rare goods within reach of a position, ordered and paged (#2094).</summary>
public static class RareListing
{
    /// <summary>The <c>order_by</c> values.</summary>
    public static readonly string[] Orders = [.. SearchSorts.For(GalaxySearchKind.Rares).Select(field => field.Name)];

    /// <summary>Whether <paramref name="orderBy"/> sorts best-first by descending value.</summary>
    public static bool DescendingByDefault(string orderBy) =>
        SearchSorts.For(GalaxySearchKind.Rares).FirstOrDefault(field => field.Name == orderBy)?.FirstDescending ?? false;

    /// <summary>
    /// Every rare within <paramref name="maxDistance"/> light years of <paramref name="origin"/>. A rare with no
    /// value under the chosen order sorts last whichever way the order runs.
    /// </summary>
    public static IReadOnlyList<RareListingRow> Within(
        StarPosition origin,
        double maxDistance,
        RareIndex? index,
        string orderBy,
        bool descending)
    {
        var rows = RareCatalogue.All
            .Select(rare =>
            {
                var row = index?.Find(rare.Symbol);

                return new RareListingRow(
                    rare,
                    row,
                    origin.DistanceTo(rare.Position),
                    row?.FarSellPrice ?? rare.FarSellPrice);
            })
            .Where(row => row.Distance <= maxDistance)
            .ToArray();

        Func<RareListingRow, object?> key = orderBy switch
        {
            "name" => row => row.Rare.Name,
            "station" => row => row.Rare.Station,
            "from_star" => row => row.Rare.DistanceToArrival,
            "supply" => row => row.Supply,
            "price" => row => row.Price,
            "profit" => row => row.Profit,
            "total" => row => row.Total,
            "reported" => row => row.ReportedAt,
            _ => row => row.Distance,
        };

        var known = rows.Where(row => key(row) is not null);
        var unknown = rows.Where(row => key(row) is null);

        var ordered = descending
            ? known.OrderByDescending(key, KeyOrder).ThenBy(row => row.Distance)
            : known.OrderBy(key, KeyOrder).ThenBy(row => row.Distance);

        return
        [
            .. ordered.ThenBy(row => row.Rare.Name, StringComparer.Ordinal),
            .. unknown.OrderBy(row => row.Distance).ThenBy(row => row.Rare.Name, StringComparer.Ordinal),
        ];
    }

    private static readonly Comparer<object?> KeyOrder = Comparer<object?>.Create((one, other) =>
        one is string text
            ? string.Compare(text, (string)other!, StringComparison.OrdinalIgnoreCase)
            : ((IComparable)one!).CompareTo(other));
}

/// <summary>The Station search that plans a Powerplay rare run from one rare.</summary>
public static class RareRunSearch
{
    public const double DefaultMinDistance = 200;

    public const string PowerState = "Unoccupied";

    public const string Service = "Market";

    /// <summary>
    /// The <c>search_stations</c> arguments for a run from <paramref name="rare"/>: stations with a market at least
    /// <paramref name="minDistance"/> light years from its system, in Unoccupied systems <paramref name="power"/> is
    /// acquiring. A null <paramref name="power"/> leaves the Power open.
    /// </summary>
    public static IReadOnlyDictionary<string, string> StationArguments(
        RareEntry rare, string? power, double minDistance = DefaultMinDistance)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["near"] = rare.System,
            ["min_distance"] = minDistance.ToString("0.##", CultureInfo.InvariantCulture),
            ["power_state"] = PowerState,
            ["services"] = Service,
        };

        if (!string.IsNullOrWhiteSpace(power))
        {
            arguments["acquiring_power"] = power;
        }

        return arguments;
    }
}
