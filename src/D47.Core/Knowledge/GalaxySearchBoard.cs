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

    /// <summary>Raised on the posting thread when a new answer lands, so a surface can redraw without polling.</summary>
    public event Action<GalaxySearchKind>? Posted;
}

/// <summary>One answered galaxy search.</summary>
/// <param name="Arguments">The tool's arguments as given, so a <c>near</c> left out still means "from here".</param>
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
    DateTimeOffset AskedAt);
