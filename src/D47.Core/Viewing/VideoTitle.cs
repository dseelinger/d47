namespace D47.Core.Viewing;

/// <summary>What a catalogue title is, as the catalogue's kind filter names it.</summary>
public enum VideoKind
{
    FeatureFilm,
    ShortFilm,
    Footage,
    Other,
}

/// <summary>The order a source is asked to sort its results in; a source that cannot sort that way keeps its own order.</summary>
public enum VideoOrder
{
    MostWatched,
    Relevance,
    Newest,
    Title,
}

/// <summary>The catalogue's filters; a null <paramref name="Kind"/> is any kind.</summary>
public sealed record VideoFilters(VideoKind? Kind = null, VideoOrder Order = VideoOrder.MostWatched);

/// <summary>
/// One title from a source's catalogue. <paramref name="Media"/> is null when the title cannot be played, and
/// <paramref name="Unplayable"/> then says why.
/// </summary>
public sealed record VideoTitle(
    string Source,
    string Id,
    string Title,
    VideoKind Kind,
    string Licence,
    TimeSpan? Length,
    Uri? Media,
    Uri? Subtitles,
    string? Credit = null,
    string? Unplayable = null)
{
    public bool Playable => Media is not null;
}

/// <summary>
/// One page of results and how many the sources hold in all. <paramref name="Problem"/> is a sentence for the
/// Commander when a source could not be searched. <paramref name="More"/> is whether a later page holds titles, which
/// the total alone does not tell when sources share a page unevenly.
/// </summary>
public sealed record VideoPage(IReadOnlyList<VideoTitle> Titles, int Total, string? Problem = null, bool More = false)
{
    public static VideoPage Empty { get; } = new([], 0);
}
