namespace D47.Core.Viewing;

/// <summary>Whether the title the player is on plays, is paused, stopped on a failure, or is not started.</summary>
public enum VideoStanding
{
    Playing,
    Paused,
    Failed,
    Idle,
}

/// <summary>The title the player is on and what it is doing.</summary>
public sealed record VideoNow(VideoTitle Title, VideoStanding Standing);

/// <summary>
/// What the video voice commands act on: Your list, Continue watching, the catalogue and the player. A video does not
/// start or resume while <paramref name="storyPlaying"/> says a story plays.
/// </summary>
public sealed class WatchDesk(
    Func<IReadOnlyList<VideoTitle>> yourList,
    Func<IReadOnlyList<VideoTitle>> carryOn,
    Func<string, CancellationToken, Task<VideoPage>> search,
    Func<VideoTitle, bool> play,
    Func<VideoNow?> now,
    Action pause,
    Func<bool> resume,
    Func<TimeSpan, bool> skip,
    Action<VideoTitle> addToList,
    Func<bool>? storyPlaying = null)
{
    public const string NoVideo = "No video is playing.";

    public const string StoryPlaying = "A story is playing.";

    public const string SayTitle = "Say watch, then the title of a video.";

    /// <summary>A video is playing, or the Commander has paused it.</summary>
    public bool Active => now() is { Standing: VideoStanding.Playing or VideoStanding.Paused };

    public bool Playing => now() is { Standing: VideoStanding.Playing };

    /// <summary>The titles in Your list and in Continue watching, for the phrases that name one.</summary>
    public IReadOnlyList<VideoTitle> Known() =>
    [
        .. yourList().Concat(carryOn()).DistinctBy(title => (title.Source, title.Id)),
    ];

    /// <summary>Plays the title called <paramref name="title"/>, from Your list first and then the ticked sources.</summary>
    public async Task<string> Watch(string? title, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return SayTitle;
        }

        if (storyPlaying?.Invoke() == true)
        {
            return StoryPlaying;
        }

        var wanted = title.Trim();
        var chosen = Pick(yourList(), wanted, playable: true) ?? Pick(carryOn(), wanted, playable: true);

        if (chosen is null)
        {
            var page = await search(wanted, cancellationToken).ConfigureAwait(false);
            chosen = Pick(page.Titles, wanted, playable: true) ?? page.Titles.FirstOrDefault(found => found.Playable);

            if (chosen is null)
            {
                return page.Problem ?? $"No video called {wanted} was found.";
            }
        }

        return play(chosen) ? $"Watching {chosen.Title}." : $"{chosen.Title} could not be played.";
    }

    public string Pause()
    {
        if (now() is not { Standing: VideoStanding.Playing or VideoStanding.Paused } current)
        {
            return NoVideo;
        }

        if (current.Standing == VideoStanding.Paused)
        {
            return "The video is already paused.";
        }

        pause();
        return "Paused.";
    }

    /// <summary>"Carry on": plays the current video on, or tries a stopped one again.</summary>
    public string Resume()
    {
        if (now() is not { } current)
        {
            return "No video is open. Say continue watching.";
        }

        if (current.Standing == VideoStanding.Playing)
        {
            return "The video is already playing.";
        }

        if (storyPlaying?.Invoke() == true)
        {
            return StoryPlaying;
        }

        if (!resume())
        {
            return $"{current.Title.Title} could not be played.";
        }

        return current.Standing switch
        {
            VideoStanding.Paused => "Resumed.",
            VideoStanding.Failed => $"Trying {current.Title.Title} again.",
            _ => $"Watching {current.Title.Title}.",
        };
    }

    /// <summary>"Continue watching": the current video while it plays, is paused or stopped, otherwise the newest in Continue watching.</summary>
    public string CarryOn()
    {
        if (now() is { Standing: VideoStanding.Playing or VideoStanding.Paused or VideoStanding.Failed })
        {
            return Resume();
        }

        if (storyPlaying?.Invoke() == true)
        {
            return StoryPlaying;
        }

        if (carryOn().FirstOrDefault() is not { } newest)
        {
            return "Nothing is waiting in Continue watching.";
        }

        return play(newest) ? $"Carrying on {newest.Title}." : $"{newest.Title} could not be played.";
    }

    /// <summary>Moves by <paramref name="seconds"/>, back when negative.</summary>
    public string Skip(int seconds)
    {
        if (seconds == 0)
        {
            return "Say how many seconds to skip.";
        }

        if (!Active)
        {
            return NoVideo;
        }

        return skip(TimeSpan.FromSeconds(seconds))
            ? $"{(seconds < 0 ? "Back" : "Forward")} {Math.Abs(seconds)} seconds."
            : "That could not be skipped.";
    }

    /// <summary>Adds the title called <paramref name="title"/> to Your list; with none, the video the player is on.</summary>
    public async Task<string> AddToList(string? title, CancellationToken cancellationToken)
    {
        VideoTitle? chosen;

        if (string.IsNullOrWhiteSpace(title))
        {
            chosen = now()?.Title;

            if (chosen is null)
            {
                return "No video is open. Say add, then a title, to your watch list.";
            }
        }
        else
        {
            var wanted = title.Trim();
            chosen = Pick(yourList(), wanted, playable: false) ?? Pick(carryOn(), wanted, playable: false);

            if (chosen is null)
            {
                var page = await search(wanted, cancellationToken).ConfigureAwait(false);
                chosen = Pick(page.Titles, wanted, playable: false) ?? page.Titles.FirstOrDefault();

                if (chosen is null)
                {
                    return page.Problem ?? $"No video called {wanted} was found.";
                }
            }
        }

        if (yourList().Any(listed => listed.Source == chosen.Source && listed.Id == chosen.Id))
        {
            return $"{chosen.Title} is already in Your list.";
        }

        addToList(chosen);
        return $"Added {chosen.Title} to Your list.";
    }

    private static VideoTitle? Pick(IReadOnlyList<VideoTitle> titles, string wanted, bool playable)
    {
        var candidates = titles.Where(title => !playable || title.Playable).ToList();

        return candidates.FirstOrDefault(title => string.Equals(title.Title, wanted, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(title => title.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase));
    }
}
