namespace D47.Core.Podcasts;

/// <summary>How an episode stands against the listening record.</summary>
public enum PodcastEpisodeMark
{
    /// <summary>Not started and not newer than anything the Commander has started.</summary>
    None,

    /// <summary>Not started, and newer than every episode of the show that has been started.</summary>
    New,

    InProgress,

    Finished,
}

/// <summary>One episode of a show as the page lists it. <see cref="Position"/> is set while in progress.</summary>
public sealed record PodcastEpisodeLine(PodcastEpisode Episode, PodcastEpisodeMark Mark, TimeSpan? Position);

/// <summary>Orders a show's episodes newest first and marks each from the listening record.</summary>
public static class PodcastBrowse
{
    /// <summary>
    /// An episode is finished or in progress by its record. Otherwise it is new when it was published after the newest
    /// episode the Commander has started; a show with nothing started has only its newest episode new.
    /// </summary>
    public static IReadOnlyList<PodcastEpisodeLine> Episodes(Uri feed, PodcastShow show, PodcastListening listening)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(listening);

        var ordered = show.Episodes.OrderByDescending(episode => episode.Published).ToList();
        DateTimeOffset? newestStarted = null;

        foreach (var episode in ordered)
        {
            if (Started(listening.Find(feed, episode.Id)))
            {
                newestStarted = episode.Published;
                break;
            }
        }

        return [.. ordered.Select((episode, index) =>
        {
            var progress = listening.Find(feed, episode.Id);

            if (progress is { Finished: true })
            {
                return new PodcastEpisodeLine(episode, PodcastEpisodeMark.Finished, null);
            }

            if (Started(progress))
            {
                return new PodcastEpisodeLine(episode, PodcastEpisodeMark.InProgress, progress!.Position);
            }

            var isNew = newestStarted is { } started ? episode.Published > started : index == 0;

            return new PodcastEpisodeLine(episode, isNew ? PodcastEpisodeMark.New : PodcastEpisodeMark.None, null);
        })];
    }

    /// <summary>How many of the show's episodes are marked new.</summary>
    public static int NewCount(IReadOnlyList<PodcastEpisodeLine> lines) =>
        lines.Count(line => line.Mark == PodcastEpisodeMark.New);

    private static bool Started(PodcastProgress? progress) =>
        progress is { Finished: true } or { Position.Ticks: > 0 };
}
