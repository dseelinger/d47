using D47.Core.Books;
using D47.Core.Podcasts;
using D47.Core.Viewing;

namespace D47.Core.Capabilities;

/// <summary>What takes bare "play" and "pause" and the media keys.</summary>
public enum MediaOwner
{
    /// <summary>Nothing else is playing or paused; the ambient music takes them.</summary>
    Music,
    Story,
    Book,
    Podcast,
    Video,
}

public static class MediaOwners
{
    /// <summary>
    /// An open story playback first, then the podcast or video that is playing, then an active book, then a
    /// paused podcast, then a paused video, else the music.
    /// </summary>
    public static MediaOwner Of(Func<bool>? storyOpen, Func<BookDesk?>? books, Func<PodcastDesk?>? podcasts, Func<WatchDesk?>? videos)
    {
        if (storyOpen?.Invoke() == true)
        {
            return MediaOwner.Story;
        }

        var book = books?.Invoke();
        var podcast = podcasts?.Invoke();
        var video = videos?.Invoke();

        if (podcast?.Player.State is { Playing: true })
        {
            return MediaOwner.Podcast;
        }

        if (video?.Playing == true)
        {
            return MediaOwner.Video;
        }

        if (book?.Active == true)
        {
            return MediaOwner.Book;
        }

        return podcast?.Active == true ? MediaOwner.Podcast : video?.Active == true ? MediaOwner.Video : MediaOwner.Music;
    }
}
