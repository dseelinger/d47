using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>Media › Movies › Catalogue searches the video sources, filters and pages the results, and opens a title (#2674).</summary>
[Trait("Category", "Integration")]
public sealed class TheMoviesCataloguePageSearchesAndPagesTests
{
    private sealed record Call(string Query, VideoFilters Filters, int Page);

    private sealed class FakeCatalogue(Func<Call, VideoPage> answer) : IVideoCatalogue
    {
        public List<Call> Calls { get; } = [];

        public Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, CancellationToken cancellationToken)
        {
            var call = new Call(query, filters, page);
            Calls.Add(call);

            return Task.FromResult(answer(call));
        }
    }

    private static VideoTitle Title(int number, bool playable = true) => new(
        VideoSources.InternetArchive,
        $"item{number}",
        $"Item {number}",
        VideoKind.Footage,
        "public domain",
        TimeSpan.FromSeconds(60 + number),
        playable ? new Uri($"https://archive.example.test/item{number}.mp4") : null,
        null,
        Unplayable: playable ? null : "offered as MOV only, which Windows can't play here");

    private static VideoPage Many(Call call) =>
        new([.. Enumerable.Range((call.Page - 1) * 25, call.Page == 1 ? 25 : 15).Select(number => Title(number))], 40, More: call.Page == 1);

    private sealed class Fixture : IDisposable
    {
        public SettingsService Settings { get; }

        public WatchHost Host { get; }

        public FakeCatalogue Catalogue { get; }

        public Fixture(Func<Call, VideoPage> answer)
        {
            var (settings, _, _) = TestSurface.Create();
            Settings = settings;
            Catalogue = new FakeCatalogue(answer);

            var files = new MemoryFileSystem();
            var store = new WatchStore("data/watch.json", files, NullLogger<WatchStore>.Instance);
            var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
            var video = new D47.App.Media.VideoPlayback(arbiter, background: work => work());

            Host = new WatchHost(
                video,
                store,
                settings,
                files,
                catalogue: true,
                readSubtitles: (_, _) => Task.FromResult<string?>(null),
                background: work => work(),
                videos: Catalogue);
        }

        public void Dispose()
        {
            Host.Video.Stop();
            Host.Dispose();
        }
    }

    private static (Window Window, PanelView Panel) Open(Fixture fixture)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableMedia(watch: fixture.Host);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.MoviesRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static IReadOnlyList<string> Drawn(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? []))];

    private static (Window Window, PanelView Panel) OpenCatalogue(Fixture fixture)
    {
        var (window, panel) = Open(fixture);

        Click(Named(panel, WatchPage.FindName));
        Settle();

        return (window, panel);
    }

    [AvaloniaFact]
    public void FindAVideoOpensTheCataloguePageBelowMoviesWithTheFirstPage()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Assert.Equal(["Movies", "Catalogue"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Equal(new Call(string.Empty, new VideoFilters(), 1), Assert.Single(fixture.Catalogue.Calls));
        Assert.Equal(25, panel.GetVisualDescendants().OfType<Button>().Count(button => button.Name == CataloguePage.RowName));

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text == "1–25 of 40");
        Assert.Contains("SHOW", drawn.Select(text => text.ToUpperInvariant()));
        Assert.Contains(drawn, text => text.Contains("public domain · footage · no captions yet", StringComparison.OrdinalIgnoreCase));

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void ASearchTypedOnTheWatchPageOpensTheCatalogueWithItsResultsAndBackReturns()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = Open(fixture);
        var page = panel.GetVisualDescendants().OfType<WatchPage>().First();

        Assert.True(page.Submit("  rocket "));
        Settle();

        Assert.Equal(["Movies", "Catalogue"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Equal("rocket", Assert.Single(fixture.Catalogue.Calls).Query);
        Assert.Contains(Drawn(panel), text => text.Contains("match “rocket”", StringComparison.OrdinalIgnoreCase));

        Click(Named(panel, CataloguePage.BackName));

        Assert.Equal(["Movies"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => button.Name == WatchPage.FindName);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void NextAndPreviousAskTheCatalogueForTheOtherPages()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Assert.False(Named(panel, CataloguePage.PreviousName).IsEnabled);
        Assert.True(Named(panel, CataloguePage.NextName).IsEnabled);

        Click(Named(panel, CataloguePage.NextName));

        Assert.Equal(2, fixture.Catalogue.Calls[^1].Page);
        Assert.Contains(Drawn(panel), text => text == "26–40 of 40");
        Assert.False(Named(panel, CataloguePage.NextName).IsEnabled);

        Click(Named(panel, CataloguePage.PreviousName));

        Assert.Equal(1, fixture.Catalogue.Calls[^1].Page);
        Assert.Contains(Drawn(panel), text => text == "1–25 of 40");

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void TheSourcesTokenWritesTheSettingAndRunsTheSearchAgain()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == "VideoToken.Sources"));

        var nasa = panel.GetVisualDescendants().OfType<CheckBox>().First(box => box.Name == VideoFilterBar.SourceName && (box.Tag as string) == VideoSources.Nasa);

        Assert.True(nasa.IsChecked);

        foreach (var box in panel.GetVisualDescendants().OfType<CheckBox>().Where(box => box.Name == VideoFilterBar.SourceName && (box.Tag as string) != VideoSources.Nasa))
        {
            box.IsChecked = false;
        }

        Settle();

        Assert.Equal([VideoSources.Nasa], fixture.Settings.Current.Watch.Sources);
        Assert.Equal(1, fixture.Catalogue.Calls[^1].Page);
        Assert.True(fixture.Catalogue.Calls.Count > 1);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void TheKindAndOrderTokensReachTheCatalogue()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == "VideoToken.Kind"));
        panel.GetVisualDescendants().OfType<D47.App.Controls.Segment>().First(segment => ReferenceEquals(segment.ItemsSource, VideoFilterBar.KindLabels)).GetVisualDescendants().OfType<RadioButton>().ElementAt(3).IsChecked = true;
        Settle();

        Assert.Equal(VideoKind.Footage, fixture.Catalogue.Calls[^1].Filters.Kind);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == "VideoToken.Order"));
        panel.GetVisualDescendants().OfType<D47.App.Controls.Segment>().First(segment => ReferenceEquals(segment.ItemsSource, VideoFilterBar.OrderLabels)).GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Settle();

        Assert.Equal(new VideoFilters(VideoKind.Footage, VideoOrder.Newest), fixture.Catalogue.Calls[^1].Filters);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void AnUnplayableRowIsDisabledAndSaysWhy()
    {
        var fixture = new Fixture(_ => new VideoPage([Title(1), Title(2, playable: false)], 2));
        var (window, panel) = OpenCatalogue(fixture);
        var rows = panel.GetVisualDescendants().OfType<Button>().Where(button => button.Name == CataloguePage.RowName).ToList();

        Assert.True(rows[0].IsEnabled);
        Assert.False(rows[1].IsEnabled);
        Assert.Contains(Drawn(panel), text => text.Contains("offered as MOV only", StringComparison.OrdinalIgnoreCase));

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void AFailedSearchShowsARedNoticeAndRetrySearchesAgain()
    {
        var failing = true;
        var fixture = new Fixture(call => failing ? VideoPage.Empty with { Problem = "Internet Archive could not be searched." } : Many(call));
        var (window, panel) = OpenCatalogue(fixture);
        var notice = panel.GetVisualDescendants().OfType<Notice>().First(found => found.Name == CataloguePage.FailedName);

        Assert.True(notice.IsVisible);
        Assert.Equal(NoticeLevel.Error, notice.Level);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), button => button.Name == CataloguePage.RowName);

        failing = false;
        Click(Named(panel, CataloguePage.RetryName));

        Assert.False(notice.IsVisible);
        Assert.Equal(25, panel.GetVisualDescendants().OfType<Button>().Count(button => button.Name == CataloguePage.RowName));

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void WatchNowOpensThePlayerBelowMoviesAndAddToMyListKeepsTheTitle()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Assert.False(Named(panel, CataloguePage.AddName).IsEnabled);
        Assert.False(Named(panel, CataloguePage.WatchName).IsEnabled);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == CataloguePage.RowName && (button.Tag as string) == "archive.item2"));
        Click(Named(panel, CataloguePage.AddName));

        Assert.Equal(["archive"], fixture.Host.YourList().Select(entry => entry.Title.Source));
        Assert.False(Named(panel, CataloguePage.AddName).IsEnabled);

        Click(Named(panel, CataloguePage.WatchName));

        Assert.Equal(["Movies", "Item 2"], panel.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
        fixture.Dispose();
    }
}
