namespace D47.Core.Knowledge;

/// <summary>How a codex entry compares with the shipped table.</summary>
public enum CodexFirst
{
    None,
    Region,
    Galaxy,
}

/// <summary>Judges a codex entry against a <see cref="CodexTable"/>.</summary>
public static class CodexFirsts
{
    private const string Organic = "$Codex_SubCategory_Organic_Structures;";

    private static readonly HashSet<string> RegionOnly = new(StringComparer.Ordinal)
    {
        "$Codex_SubCategory_Geology_and_Anomalies;",
        "$Codex_SubCategory_Guardian;",
        "$Codex_SubCategory_Gas_Giants;",
    };

    /// <summary>
    /// Organic structures: region first when the row does not list the region, galaxy first with no row or no regions.
    /// Geology and anomalies, Guardian and gas giants: region first only, and only when a row exists without the region.
    /// Any other subcategory: none.
    /// </summary>
    public static CodexFirst Judge(CodexTable table, long entryId, int regionId, string? subCategory)
    {
        ArgumentNullException.ThrowIfNull(table);

        var regions = table.Entry(entryId)?.Regions;

        if (string.Equals(subCategory, Organic, StringComparison.Ordinal))
        {
            return regions is not { Count: > 0 } ? CodexFirst.Galaxy
                : regions.ContainsKey(regionId) ? CodexFirst.None
                : CodexFirst.Region;
        }

        return subCategory is not null && RegionOnly.Contains(subCategory) && regions is not null && !regions.ContainsKey(regionId)
            ? CodexFirst.Region
            : CodexFirst.None;
    }
}
