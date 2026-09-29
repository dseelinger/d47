namespace D47.Core.Capabilities;

/// <summary>Names for a spoken answer, cut to the first few.</summary>
public static class SpokenList
{
    public const int Limit = 3;

    /// <summary>"a", "a and b", "a, b and c", then "a, b and c, and N more".</summary>
    public static string Names(IReadOnlyList<string> items)
    {
        var shown = items.Take(Limit).ToList();

        var named = shown.Count < 2
            ? string.Concat(shown)
            : $"{string.Join(", ", shown.Take(shown.Count - 1))} and {shown[^1]}";

        return items.Count > Limit ? $"{named}, and {items.Count - Limit} more" : named;
    }
}
