using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Media tab: Music, Podcasts, Books and Movies; Podcasts has its page, the rest are empty until their own issues fill them.</summary>
internal sealed class MediaFurnishing(IPanelTabHost host)
{
    public const string MusicRoot = "media-music";
    public const string PodcastsRoot = "media-podcasts";
    public const string BooksRoot = "media-books";
    public const string MoviesRoot = "media-movies";

    private bool _furnished;

    /// <param name="podcasts">What the Podcasts page reads, or null where the surface has none.</param>
    /// <param name="settingsStrip">Builds a tab's own settings strip for a root key, or null.</param>
    public void Enable(PodcastsHost? podcasts = null, Func<string, Control?>? settingsStrip = null)
    {
        if (_furnished)
        {
            return;
        }

        _furnished = true;

        var help = D47.Core.Capabilities.Builtin.InterfaceCapability.Id;

        host.Furnish(
            PanelTab.Media,
            crumb => Build(crumb, podcasts, settingsStrip),
            new NavCrumb(MusicRoot, "Music") { Help = help },
            new NavCrumb(PodcastsRoot, "Podcasts") { Help = D47.Core.Capabilities.Builtin.PodcastCapability.Id },
            new NavCrumb(BooksRoot, "Books") { Help = help },
            new NavCrumb(MoviesRoot, "Movies") { Help = help, Spoken = ["video", "videos"] });
    }

    private Control Build(NavCrumb crumb, PodcastsHost? podcasts, Func<string, Control?>? settingsStrip)
    {
        if (podcasts is null)
        {
            return new Border();
        }

        return crumb.Key switch
        {
            PodcastsRoot => new PodcastsPage(
                podcasts,
                host.Nav,
                settingsStrip?.Invoke(PodcastsRoot),
                host.Model?.WaitSound,
                host.Headset),
            PodcastsPage.AddKey => new PodcastAddPage(podcasts, host.Nav, host.Model?.WaitSound, host.Headset),
            _ => new Border(),
        };
    }
}
