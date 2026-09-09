namespace D47.Core.Interface;

/// <summary>Where a hit sits in a page's text, and how much of it the hit is.</summary>
public readonly record struct SearchMatch(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Finding a query in a page and keeping track of which hit is current (Phase 12, "Search whichever tab
/// you are looking at").
/// </summary>
public static class TextSearch
{
    /// <summary>Every hit, in order, ignoring case.</summary>
    public static IReadOnlyList<SearchMatch> Find(string? text, string? query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
        {
            return [];
        }

        var found = new List<SearchMatch>();

        for (var at = 0; at <= text.Length - query.Length;)
        {
            var hit = text.IndexOf(query, at, StringComparison.OrdinalIgnoreCase);

            if (hit < 0)
            {
                break;
            }

            found.Add(new SearchMatch(hit, query.Length));
            at = hit + query.Length;
        }

        return found;
    }

    /// <summary>Which hit a remembered character offset now names.</summary>
    public static int Track(IReadOnlyList<SearchMatch> matches, int offset)
    {
        if (matches.Count == 0)
        {
            return -1;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Start >= offset)
            {
                return i;
            }
        }

        return matches.Count - 1;
    }

    /// <summary>One step through the hits, wrapping at both ends.</summary>
    public static int Step(int count, int current, int by)
    {
        if (count <= 0)
        {
            return -1;
        }

        var next = (current + by) % count;

        return next < 0 ? next + count : next;
    }

    /// <summary>How the count is said: "3 of 17", or that there are none.</summary>
    public static string Describe(int count, int current) => count switch
    {
        0 => "no matches",
        _ => $"{current + 1} of {count}",
    };
}
