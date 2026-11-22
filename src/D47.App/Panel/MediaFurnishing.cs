using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Media tab: Music, Podcasts, Books and Movies, each an empty page until its own issue fills it.</summary>
internal sealed class MediaFurnishing(IPanelTabHost host)
{
    public const string MusicRoot = "media-music";
    public const string PodcastsRoot = "media-podcasts";
    public const string BooksRoot = "media-books";
    public const string MoviesRoot = "media-movies";

    private bool _furnished;

    public void Enable()
    {
        if (_furnished)
        {
            return;
        }

        _furnished = true;

        var help = D47.Core.Capabilities.Builtin.InterfaceCapability.Id;

        host.Furnish(
            PanelTab.Media,
            _ => new Border(),
            new NavCrumb(MusicRoot, "Music") { Help = help },
            new NavCrumb(PodcastsRoot, "Podcasts") { Help = help },
            new NavCrumb(BooksRoot, "Books") { Help = help },
            new NavCrumb(MoviesRoot, "Movies") { Help = help, Spoken = ["video", "videos"] });
    }
}
