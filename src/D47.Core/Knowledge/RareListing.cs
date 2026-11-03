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

/// <summary>The rare goods within reach of a position, ordered and paged (#2094).</summary>
public static class RareListing
{
    public static readonly string[] Orders = ["distance", "price", "profit", "total"];

    /// <summary>Whether <paramref name="orderBy"/> sorts best-first by descending value.</summary>
    public static bool DescendingByDefault(string orderBy) => orderBy is "profit" or "total";

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
            .Where(row => row.Distance <= maxDistance);

        Func<RareListingRow, double?> key = orderBy switch
        {
            "price" => row => row.Price,
            "profit" => row => row.Profit,
            "total" => row => row.Total,
            _ => row => row.Distance,
        };

        var known = rows.Where(row => key(row) is not null);
        var unknown = rows.Where(row => key(row) is null);

        var ordered = descending
            ? known.OrderByDescending(key).ThenBy(row => row.Distance)
            : known.OrderBy(key).ThenBy(row => row.Distance);

        return
        [
            .. ordered.ThenBy(row => row.Rare.Name, StringComparer.Ordinal),
            .. unknown.OrderBy(row => row.Distance).ThenBy(row => row.Rare.Name, StringComparer.Ordinal),
        ];
    }
}
