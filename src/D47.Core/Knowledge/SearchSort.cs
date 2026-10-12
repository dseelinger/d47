namespace D47.Core.Knowledge;

/// <summary>One thing a galaxy search can be sorted by: the tool's name for it and the field the service sorts on.</summary>
/// <param name="Name">The <c>order_by</c> value.</param>
/// <param name="Field">The service's sort field.</param>
/// <param name="FirstDescending">Whether the field's first direction is descending.</param>
/// <param name="Ascending">The answer head's words for ascending.</param>
/// <param name="Descending">The answer head's words for descending.</param>
public sealed record SearchSortField(string Name, string Field, bool FirstDescending, string Ascending, string Descending)
{
    /// <summary>Sorts in the first direction only: the field is not a service field and has no reverse.</summary>
    public bool OneWay { get; init; }
}

/// <summary>A search sorted by <see cref="Field"/>, descending or not.</summary>
public sealed record SearchSort(SearchSortField Field, bool Descending)
{
    /// <summary>The words after the count in the answer head, or null for the default nearest-first order.</summary>
    public string? Words => Field.Name == SearchSorts.Distance && !Descending
        ? null
        : Descending ? Field.Descending : Field.Ascending;
}

/// <summary>The fields each kind of search sorts on, and the reading of <c>order_by</c> and <c>order</c>.</summary>
public static class SearchSorts
{
    public const string Distance = "distance";

    /// <summary>The <c>order_by</c> value that keeps the richest in the asked-for material.</summary>
    public const string Material = "material";

    private static SearchSortField Text(string name, string field, string label) =>
        new(name, field, false, $"{label} A–Z", $"{label} Z–A");

    private static readonly SearchSortField Near = new(Distance, "distance", false, "Nearest first", "Farthest first");

    private static readonly SearchSortField FromStar =
        new("from_star", "distance_to_arrival", false, "Nearest its star first", "Farthest from its star first");

    private static readonly SearchSortField Updated =
        new("updated", "updated_at", true, "Oldest report first", "Newest report first");

    private static readonly IReadOnlyList<SearchSortField> Systems =
    [
        Near,
        Text("name", "name", "Name"),
        Text("allegiance", "allegiance", "Allegiance"),
        Text("government", "government", "Government"),
        Text("economy", "primary_economy", "Economy"),
        Text("security", "security", "Security"),
        Text("power", "controlling_power", "Power"),
        new("population", "population", true, "Least populous", "Most populous"),
        Updated,
    ];

    private static readonly IReadOnlyList<SearchSortField> Stations =
    [
        Near,
        Text("name", "name", "Station name"),
        Text("system", "system_name", "System name"),
        FromStar,
        Updated,
    ];

    private static readonly IReadOnlyList<SearchSortField> Bodies =
    [
        Near,
        Text("name", "name", "Body name"),
        Text("system", "system_name", "System name"),
        FromStar,
        new("gravity", "gravity", true, "Lowest gravity first", "Highest gravity first"),
        new(Material, "distance", true, "Richest first", "Richest first") { OneWay = true },
    ];

    /// <summary>The fields <paramref name="kind"/> sorts on, nearest first among them.</summary>
    public static IReadOnlyList<SearchSortField> For(GalaxySearchKind kind) => kind switch
    {
        GalaxySearchKind.Stations => Stations,
        GalaxySearchKind.Bodies => Bodies,
        _ => Systems,
    };

    /// <summary>The <c>order_by</c> values, comma-separated, for a tool description.</summary>
    public static string Names(GalaxySearchKind kind) => string.Join(", ", For(kind).Select(field => field.Name));

    /// <summary>The service's sort field and direction; distance ascending when <paramref name="sort"/> is null.</summary>
    public static (string Field, string Direction) Wire(SearchSort? sort) =>
        sort is null ? ("distance", "asc") : (sort.Field.Field, sort.Descending ? "desc" : "asc");

    /// <summary>
    /// Reads <paramref name="orderBy"/> and <paramref name="order"/> (<c>asc</c> or <c>desc</c>). Neither given
    /// is the default order, <paramref name="sort"/> null; <paramref name="order"/> alone is refused.
    /// </summary>
    public static bool TryParse(
        GalaxySearchKind kind,
        string? orderBy,
        string? order,
        out SearchSort? sort,
        out string failure)
    {
        sort = null;
        failure = string.Empty;

        var hasField = !string.IsNullOrWhiteSpace(orderBy);
        var hasOrder = !string.IsNullOrWhiteSpace(order);

        if (!hasField)
        {
            if (!hasOrder)
            {
                return true;
            }

            failure = $"order says which way, and needs order_by to say what to sort by: {Names(kind)}.";
            return false;
        }

        var fields = For(kind);

        if (fields.FirstOrDefault(field => string.Equals(field.Name, orderBy!.Trim(), StringComparison.OrdinalIgnoreCase)) is not { } chosen)
        {
            failure = $"I can't sort by '{orderBy!.Trim()}'. It has to be one of: {Names(kind)}.";
            return false;
        }

        var descending = chosen.FirstDescending;

        if (hasOrder)
        {
            var text = order!.Trim();

            if (string.Equals(text, "asc", StringComparison.OrdinalIgnoreCase))
            {
                descending = false;
            }
            else if (string.Equals(text, "desc", StringComparison.OrdinalIgnoreCase))
            {
                descending = true;
            }
            else
            {
                failure = $"order is asc or desc, not '{text}'.";
                return false;
            }

            if (chosen.OneWay && descending != chosen.FirstDescending)
            {
                failure = $"Sorting by {chosen.Name} only goes {(chosen.FirstDescending ? "desc" : "asc")}.";
                return false;
            }
        }

        sort = new SearchSort(chosen, descending);
        return true;
    }

    /// <summary>The sort a search was asked with, or null where it was asked with none or an unreadable one.</summary>
    public static SearchSort? Read(GalaxySearchKind kind, IReadOnlyDictionary<string, string> arguments) =>
        TryParse(kind, arguments.GetValueOrDefault("order_by"), arguments.GetValueOrDefault("order"), out var sort, out _)
            ? sort
            : null;
}
