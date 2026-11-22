using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Media tab: Music, Podcasts, Books and Movies; Music, Podcasts and Books have pages, the rest are empty until their own issues fill them.</summary>
internal sealed class MediaFurnishing(IPanelTabHost host)
{
    public const string MusicRoot = "media-music";
    public const string PodcastsRoot = "media-podcasts";
    public const string BooksRoot = "media-books";
    public const string MoviesRoot = "media-movies";

    private bool _furnished;

    /// <summary>The book on the mini panel, once Books is furnished.</summary>
    public BookMini? BooksMini { get; private set; }

    /// <param name="music">What the Music page reads, or null where the surface has none.</param>
    /// <param name="podcasts">What the Podcasts page reads, or null where the surface has none.</param>
    /// <param name="books">What the Books page reads, or null where the surface has none.</param>
    /// <param name="settingsStrip">Builds a tab's own settings strip for a root key, or null.</param>
    public void Enable(MusicHost? music = null, PodcastsHost? podcasts = null, BooksHost? books = null, Func<string, Control?>? settingsStrip = null)
    {
        if (_furnished)
        {
            return;
        }

        _furnished = true;
        BooksMini = books is null ? null : new BookMini(books);

        var help = D47.Core.Capabilities.Builtin.InterfaceCapability.Id;

        host.Furnish(
            PanelTab.Media,
            crumb => Build(crumb, music, podcasts, books, settingsStrip),
            new NavCrumb(MusicRoot, "Music") { Help = D47.Core.Capabilities.Builtin.AudioCapability.Id },
            new NavCrumb(PodcastsRoot, "Podcasts") { Help = D47.Core.Capabilities.Builtin.PodcastCapability.Id },
            new NavCrumb(BooksRoot, "Books") { Help = help },
            new NavCrumb(MoviesRoot, "Movies") { Help = help, Spoken = ["video", "videos"] });
    }

    private Control Build(NavCrumb crumb, MusicHost? music, PodcastsHost? podcasts, BooksHost? books, Func<string, Control?>? settingsStrip) =>
        crumb.Key switch
        {
            MusicRoot when music is not null && host.Model is { } model => new MusicPage(
                music,
                model,
                host.Nav,
                settingsStrip?.Invoke(MusicRoot),
                host.Headset),
            PodcastsRoot when podcasts is not null => new PodcastsPage(
                podcasts,
                host.Nav,
                settingsStrip?.Invoke(PodcastsRoot),
                host.Model?.WaitSound,
                host.Headset),
            PodcastsPage.AddKey when podcasts is not null => new PodcastAddPage(podcasts, host.Nav, host.Model?.WaitSound, host.Headset),
            BooksRoot when books is not null => new BooksPage(
                books,
                host.Nav,
                host.Prompts,
                host.OpenSettingsPlace,
                settingsStrip?.Invoke(BooksRoot),
                host.Headset),
            BookAddPage.Key when books is { Folder: not null } => new BookAddPage(books, host.Nav, host.Headset, host.Model?.WaitSound),
            _ when books is not null && BookPage.SourceOf(books.Shelf, crumb.Key) is { } source => new BookPage(
                books,
                host.Nav,
                source,
                host.OpenSettingsPlace,
                host.Headset),
            _ => new Border(),
        };
}
