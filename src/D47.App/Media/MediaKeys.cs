using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Capabilities;
using D47.Core.Podcasts;
using D47.Core.Viewing;

namespace D47.App.Media;

public enum MediaKey
{
    Play,
    Pause,
    Next,
    Previous,
}

/// <summary>What each media key does to each owner; <see cref="MediaSession"/> fills it from the host.</summary>
internal interface IMediaKeyTargets
{
    void StoryResume();

    void StoryPause();

    void StorySkip();

    string BookResume();

    string BookPause();

    string BookNextChapter();

    string BookBackAParagraph();

    string VideoResume();

    string VideoPause();

    string VideoSkip(int seconds);

    string PodcastResume();

    string PodcastPause();

    string PodcastSkipBack();

    Task<string> PodcastNext();

    string MusicControl(MusicAction action);
}

/// <summary>What the flyout reads for each owner.</summary>
internal interface IMediaFlyoutSource
{
    string? StoryTitle { get; }

    bool StoryPaused { get; }

    BookNow? Book { get; }

    PodcastPlayerState? Podcast { get; }

    VideoTitle? Video { get; }

    bool VideoPlaying { get; }

    MusicState Music { get; }
}

public enum MediaFlyoutStatus
{
    Playing,
    Paused,
    Stopped,
}

/// <summary>A null <paramref name="Title"/> clears the flyout.</summary>
internal sealed record MediaFlyout(MediaFlyoutStatus Status, string? Title, string? Artist);

internal static class MediaKeys
{
    public const string MusicArtist = "Directive 47";

    /// <summary>
    /// Applies <paramref name="key"/> to <paramref name="owner"/>. Returns the result for the log, or null when
    /// the owner ignores the key. The task completes later for a podcast's next episode.
    /// </summary>
    public static Task<string>? Press(MediaOwner owner, MediaKey key, IMediaKeyTargets targets)
    {
        switch (owner)
        {
            case MediaOwner.Story:
                return PressOnStory(key, targets);

            case MediaOwner.Book:
                return Done(key switch
                {
                    MediaKey.Play => targets.BookResume(),
                    MediaKey.Pause => targets.BookPause(),
                    MediaKey.Next => targets.BookNextChapter(),
                    _ => targets.BookBackAParagraph(),
                });

            case MediaOwner.Video:
                return Done(key switch
                {
                    MediaKey.Play => targets.VideoResume(),
                    MediaKey.Pause => targets.VideoPause(),
                    MediaKey.Next => targets.VideoSkip(30),
                    _ => targets.VideoSkip(-30),
                });

            case MediaOwner.Podcast:
                return key switch
                {
                    MediaKey.Play => Done(targets.PodcastResume()),
                    MediaKey.Pause => Done(targets.PodcastPause()),
                    MediaKey.Previous => Done(targets.PodcastSkipBack()),
                    _ => targets.PodcastNext().ContinueWith(
                        done => done.IsCompletedSuccessfully ? done.Result : "failed",
                        TaskScheduler.Default),
                };

            default:
                return Done(targets.MusicControl(key switch
                {
                    MediaKey.Play => MusicAction.Resume,
                    MediaKey.Pause => MusicAction.Pause,
                    MediaKey.Next => MusicAction.Next,
                    _ => MusicAction.Previous,
                }));
        }
    }

    public static MediaFlyout Flyout(MediaOwner owner, IMediaFlyoutSource source)
    {
        switch (owner)
        {
            case MediaOwner.Story when source.StoryTitle is { } story:
                return Item(story, "Story", !source.StoryPaused);

            case MediaOwner.Book when source.Book is { } book:
                return Item(book.Title, book.ChapterTitle, !book.Paused);

            case MediaOwner.Podcast when source.Podcast is { } episode:
                return Item(episode.Episode.Title, episode.Show, episode.Playing);

            case MediaOwner.Video when source.Video is { } video:
                return Item(video.Title, "Video", source.VideoPlaying);

            default:
                var music = source.Music;
                var status = music switch
                {
                    { Playing: true } => MediaFlyoutStatus.Playing,
                    { Paused: true } or { Idle: true } => MediaFlyoutStatus.Paused,
                    _ => MediaFlyoutStatus.Stopped,
                };

                return music.Track is { } track
                    ? new MediaFlyout(status, Path.GetFileNameWithoutExtension(track), MusicArtist)
                    : new MediaFlyout(status, null, null);
        }
    }

    private static Task<string>? PressOnStory(MediaKey key, IMediaKeyTargets targets)
    {
        switch (key)
        {
            case MediaKey.Play:
                targets.StoryResume();
                break;

            case MediaKey.Pause:
                targets.StoryPause();
                break;

            case MediaKey.Next:
                targets.StorySkip();
                break;

            default:
                return null;
        }

        return Done("story");
    }

    private static MediaFlyout Item(string title, string artist, bool playing) =>
        new(playing ? MediaFlyoutStatus.Playing : MediaFlyoutStatus.Paused, title, artist);

    private static Task<string> Done(string result) => Task.FromResult(result);
}
