namespace D47.Core.Interface;

/// <summary>The query, its hits in a page and the current hit, which stays on the same text as the page grows.</summary>
public sealed class PageSearch
{
    private int _offset;

    public string Query { get; private set; } = string.Empty;

    public bool Active => Query.Length > 0;

    public IReadOnlyList<SearchMatch> Matches { get; private set; } = [];

    /// <summary>Index into <see cref="Matches"/>; -1 with no current hit.</summary>
    public int Hit { get; private set; } = -1;

    /// <summary>Sets a new query, which starts at the first hit.</summary>
    public void Ask(string? query)
    {
        Query = query ?? string.Empty;
        _offset = 0;
    }

    /// <summary>Finds the query in the page's text and keeps the current hit on the text it was on.</summary>
    public void Find(string text)
    {
        Matches = TextSearch.Find(text, Query);
        Hit = TextSearch.Track(Matches, _offset);

        if (Hit >= 0)
        {
            _offset = Matches[Hit].Start;
        }
    }

    /// <summary>No page to search: no hits, and the tracked offset is kept.</summary>
    public void Forget()
    {
        Matches = [];
        Hit = -1;
    }

    /// <summary>Moves the current hit, wrapping; false with no hits.</summary>
    public bool Step(int by)
    {
        if (Matches.Count == 0)
        {
            return false;
        }

        Hit = TextSearch.Step(Matches.Count, Hit, by);
        _offset = Matches[Hit].Start;
        return true;
    }

    public string Describe() => TextSearch.Describe(Matches.Count, Hit);
}
