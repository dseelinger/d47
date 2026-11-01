namespace D47.Core.Knowledge;

/// <summary>The last answer of each galaxy search kind, so the spoken one and the drawn one are one answer.</summary>
public sealed class GalaxySearchBoard
{
    private readonly Lock _gate = new();

    private readonly Dictionary<GalaxySearchKind, GalaxySearchPosting> _last = [];

    /// <summary>The last answer of <paramref name="kind"/>, or null if none has been given.</summary>
    public GalaxySearchPosting? Last(GalaxySearchKind kind)
    {
        lock (_gate)
        {
            return _last.GetValueOrDefault(kind);
        }
    }

    /// <summary>Keeps <paramref name="posting"/> as its kind's last answer, then raises <see cref="Posted"/>.</summary>
    public void Post(GalaxySearchPosting posting)
    {
        lock (_gate)
        {
            _last[posting.Kind] = posting;
        }

        Posted?.Invoke(posting.Kind);
    }

    /// <summary>
    /// Adds a later page to its kind's last answer and raises <see cref="Extended"/>. The last answer must have
    /// the same arguments and hold one page fewer than <paramref name="page"/>; otherwise nothing changes and
    /// this returns false.
    /// </summary>
    public bool Extend(GalaxySearchPosting page)
    {
        lock (_gate)
        {
            if (_last.GetValueOrDefault(page.Kind) is not { } last
                || last.Pages != page.Pages - 1
                || !SameArguments(last.Arguments, page.Arguments)
                || Joined(last.Result, page.Result) is not { } result)
            {
                return false;
            }

            _last[page.Kind] = last with { Result = result, Pages = page.Pages, More = page.More };
        }

        Extended?.Invoke(page.Kind);
        return true;
    }

    /// <summary>Raised on the posting thread when a new answer lands, so a surface can redraw without polling.</summary>
    public event Action<GalaxySearchKind>? Posted;

    /// <summary>Raised on the posting thread when a later page is added to the last answer.</summary>
    public event Action<GalaxySearchKind>? Extended;

    private static bool SameArguments(IReadOnlyDictionary<string, string> one, IReadOnlyDictionary<string, string> other) =>
        one.Count == other.Count
        && one.All(pair => other.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

    /// <summary>The rows of both, in order, with the later page's total; null where the two are not one kind of result.</summary>
    private static object? Joined(object first, object later) => (first, later) switch
    {
        (GalaxySearchResult held, GalaxySearchResult added) =>
            added with { Reference = held.Reference, Systems = [.. held.Systems, .. added.Systems] },
        (StationSearchResult held, StationSearchResult added) => added with
        {
            Reference = held.Reference,
            Stations =
            [
                .. held.Stations,
                .. added.Stations.Where(station =>
                    station.MarketId is not { } market || !held.Stations.Any(shown => shown.MarketId == market)),
            ],
            LeftOut = held.LeftOut + added.LeftOut,
        },
        (BodySearchResult held, BodySearchResult added) =>
            added with { Reference = held.Reference, Bodies = [.. held.Bodies, .. added.Bodies] },
        _ => null,
    };
}

/// <summary>One answered galaxy search.</summary>
/// <param name="Arguments">
/// The tool's arguments as given, without <c>page</c>, so a <c>near</c> left out still means "from here".
/// </param>
/// <param name="Reference">The system distances were measured from, as the service resolved it.</param>
/// <param name="Result">
/// A <see cref="GalaxySearchResult"/>, <see cref="StationSearchResult"/> or <see cref="BodySearchResult"/>,
/// by <paramref name="Kind"/>.
/// </param>
/// <param name="Answer">What was said.</param>
public sealed record GalaxySearchPosting(
    GalaxySearchKind Kind,
    IReadOnlyDictionary<string, string> Arguments,
    string? Reference,
    object Result,
    string Answer,
    DateTimeOffset AskedAt)
{
    /// <summary>The sentences <see cref="Answer"/> opens with that are about how the search was read, such as a corrected faction name.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>How many pages of the search <see cref="Result"/> holds.</summary>
    public int Pages { get; init; } = 1;

    /// <summary>Whether the service has rows past the ones <see cref="Result"/> holds.</summary>
    public bool More { get; init; }
}
