namespace D47.Core.Viewing;

/// <summary>
/// The Blender open movies whose files on download.blender.org open and decode in Media Foundation, searched on this
/// PC. Blender has no search service, so no search text leaves the machine.
/// </summary>
public sealed class BlenderOpenMovies : IVideoSource
{
    /// <summary>Each film with its year and the words a search matches besides its title.</summary>
    public static IReadOnlyList<(VideoTitle Title, int Year, string Words)> Films { get; } =
    [
        (new VideoTitle(
            VideoSources.Blender,
            "elephants-dream",
            "Elephants Dream",
            VideoKind.ShortFilm,
            "CC BY 2.5",
            TimeSpan.FromSeconds(658.32),
            new Uri("https://download.blender.org/ED/elephantsdream-1024-h264-st-aac.mov"),
            Subtitles: null,
            Credit: "Blender Foundation and the Netherlands Media Art Institute, elephantsdream.org"),
         2006,
         "open movie animation surreal machine orange"),
        (new VideoTitle(
            VideoSources.Blender,
            "tears-of-steel",
            "Tears of Steel",
            VideoKind.ShortFilm,
            "CC BY 3.0",
            TimeSpan.FromSeconds(734.17),
            new Uri("https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mov"),
            new Uri("https://download.blender.org/demo/movies/ToS/subtitles/TOS-en.srt"),
            Credit: "Blender Foundation, mango.blender.org"),
         2012,
         "open movie science fiction robots amsterdam mango"),
    ];

    public string Id => VideoSources.Blender;

    public Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentNullException.ThrowIfNull(filters);

        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matched = Films
            .Where(film => filters.Kind is null || film.Title.Kind == filters.Kind)
            .Where(film => words.All(word =>
                film.Title.Title.Contains(word, StringComparison.OrdinalIgnoreCase)
                || film.Words.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        IEnumerable<(VideoTitle Title, int Year, string Words)> ordered = filters.Order switch
        {
            VideoOrder.Newest => matched.OrderByDescending(film => film.Year),
            VideoOrder.Title => matched.OrderBy(film => film.Title.Title, StringComparer.OrdinalIgnoreCase),
            _ => matched,
        };

        return Task.FromResult(new VideoPage(
            [.. ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(film => film.Title)],
            matched.Count));
    }
}
