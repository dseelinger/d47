namespace D47.Core.Interface;

/// <summary>Where a hit sits in a page's text, and how much of it the hit is.</summary>
public readonly record struct SearchMatch(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Finding a query in a page and keeping track of which hit is current
/// (list.md Phase 12, "Search whichever tab you are looking at").
/// <para>
/// Offset arithmetic and nothing else. It lives here rather than in the view for the reason
/// everything in Core does: the awkward parts of this feature — the current hit surviving the
/// log growing underneath it, stepping wrapping at both ends, a query that matches nothing —
/// are all arithmetic, and arithmetic that needs a control instantiated to be checked is
/// arithmetic that gets checked once.
/// </para>
/// </summary>
public static class TextSearch
{
    /// <summary>
    /// Every hit, in order, ignoring case.
    /// <para>
    /// Non-overlapping, and the search resumes past the end of each hit rather than one
    /// character on: "aa" in "aaaa" is two hits, which is what a reader counting them would
    /// say, and stepping through four overlapping ones would visit the same text twice.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Which hit a remembered character offset now names.
    /// <para>
    /// The current hit is held as an offset into the page rather than as an index into the
    /// list, and this is why: the log page is re-read whenever it is looked at and grows
    /// underneath the reader, so an index means "the third hit" — which becomes a different
    /// piece of text the moment a line arrives — while an offset means "that hit", and it
    /// keeps meaning it.
    /// </para>
    /// <para>
    /// The hit at the offset if there is one, otherwise the first one after it, otherwise the
    /// last — so a hit that has been edited away hands the reader the nearest thing to where
    /// they were rather than sending them back to the top.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// One step through the hits, wrapping at both ends. Wrapping rather than stopping: the
    /// last hit with a dead "next" beside a count saying there are seventeen reads as a broken
    /// button, and every search box a Commander has met wraps.
    /// </summary>
    public static int Step(int count, int current, int by)
    {
        if (count <= 0)
        {
            return -1;
        }

        var next = (current + by) % count;

        return next < 0 ? next + count : next;
    }

    /// <summary>
    /// How the count is said: "3 of 17", or that there are none. The wording is here rather
    /// than in the view because it is the one part of this a test can read back.
    /// </summary>
    public static string Describe(int count, int current) => count switch
    {
        0 => "no matches",
        _ => $"{current + 1} of {count}",
    };
}
