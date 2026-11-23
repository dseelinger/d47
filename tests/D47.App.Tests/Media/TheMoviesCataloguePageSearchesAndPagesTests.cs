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

        public TaskCompletionSource<bool>? Hold { get; set; }

        public Func<Call, bool> Holds { get; set; } = _ => false;

        public async Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, CancellationToken cancellationToken)
        {
            var call = new Call(query, filters, page);
            Calls.Add(call);

            if (Hold is { } hold && Holds(call))
            {
                await hold.Task;
            }

            return answer(call);
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

    private static (Window Window, PanelView Panel) Open(Fixture fixture, bool headset = false)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

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

    private static (Window Window, PanelView Panel) OpenCatalogue(Fixture fixture, bool headset = false)
    {
        var (window, panel) = Open(fixture, headset);

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

        Assert.Contains(drawn, text => text == "25 of 40 videos");
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

    private static ScrollViewer ListScroll(Control root) =>
        root.GetVisualDescendants().OfType<ScrollViewer>().First(scroll => scroll.GetVisualDescendants().OfType<Button>().Any(button => button.Name == CataloguePage.RowName));

    private static int RowCount(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Count(button => button.Name == CataloguePage.RowName);

    private static void ScrollToEnd(Control root)
    {
        var scroll = ListScroll(root);

        scroll.Offset = new Avalonia.Vector(0, scroll.Extent.Height);
        Settle();
    }

    private static TextBox SearchBox(Control root) =>
        root.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == CataloguePage.SearchName);

    [AvaloniaFact]
    public void TheDesktopCatalogueHasNoPagerAndTheTitleLineHasNoSearchField()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Control>(), control => control.Name is CataloguePage.PagerName or CataloguePage.PreviousName or CataloguePage.NextName);
        Assert.Equal(CataloguePage.Placeholder, SearchBox(panel).PlaceholderText);
        Assert.False(panel.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == "SearchInput").IsVisible);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void ScrollingToTheEndAddsTheNextTitlesUnderTheRowsDrawnAndKeepsTheFixedPart()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == CataloguePage.RowName && (button.Tag as string) == "archive.item2"));

        var first = panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == CataloguePage.RowName && (button.Tag as string) == "archive.item0");

        ScrollToEnd(panel);

        Assert.Equal(2, fixture.Catalogue.Calls[^1].Page);
        Assert.Equal(40, RowCount(panel));
        Assert.Contains(Drawn(panel), text => text == "40 of 40 videos");
        Assert.True(SearchBox(panel).IsEffectivelyVisible);
        Assert.True(Named(panel, CataloguePage.WatchName).IsEffectivelyVisible);
        Assert.DoesNotContain(SearchBox(panel), ListScroll(panel).GetVisualDescendants());
        var count = panel.GetVisualDescendants().OfType<TextBlock>().First(block => string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? []).Contains("of 40 videos", StringComparison.Ordinal));

        Assert.DoesNotContain(count, ListScroll(panel).GetVisualDescendants());
        Assert.Same(first, panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == CataloguePage.RowName));
        Assert.Contains(D47.App.Theming.ListRow.SelectedClass, panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == CataloguePage.RowName && (button.Tag as string) == "archive.item2").Classes);

        ScrollToEnd(panel);

        Assert.Equal(2, fixture.Catalogue.Calls.Count);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void ALaterPageThatFailsKeepsTheRowsAndRetryAsksForThatPageAgain()
    {
        var failing = true;
        var fixture = new Fixture(call => call.Page == 2 && failing ? VideoPage.Empty with { Problem = "Internet Archive could not be searched." } : Many(call));
        var (window, panel) = OpenCatalogue(fixture);

        ScrollToEnd(panel);

        var notice = panel.GetVisualDescendants().OfType<Notice>().First(found => found.Name == CataloguePage.FailedName);

        Assert.True(notice.IsVisible);
        Assert.Equal(25, RowCount(panel));

        ScrollToEnd(panel);

        Assert.Equal(2, fixture.Catalogue.Calls.Count);

        failing = false;
        Click(Named(panel, CataloguePage.RetryName));

        Assert.Equal(2, fixture.Catalogue.Calls[^1].Page);
        Assert.False(notice.IsVisible);
        Assert.Equal(40, RowCount(panel));

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void EnterInTheSearchBoxSearchesFromTheFirstPageWithNothingSelectedAndDownFocusesTheFirstRow()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == CataloguePage.RowName && (button.Tag as string) == "archive.item2"));
        ScrollToEnd(panel);

        var box = SearchBox(panel);

        box.Text = " rocket ";
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        Settle();

        Assert.Equal(new Call("rocket", new VideoFilters(), 1), fixture.Catalogue.Calls[^1]);
        Assert.Equal(25, RowCount(panel));
        Assert.Equal(0, ListScroll(panel).Offset.Y);
        Assert.False(Named(panel, CataloguePage.WatchName).IsEnabled);
        Assert.Contains(Drawn(panel), text => text.Contains("match “rocket”", StringComparison.OrdinalIgnoreCase));

        box.Focus();
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Down });
        Settle();

        Assert.Equal("archive.item0", (TopLevel.GetTopLevel(panel)!.FocusManager!.GetFocusedElement() as Button)?.Tag);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void ScrollingWhileANewSearchIsOnItsWayAsksForNothingMore()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);
        var hold = fixture.Catalogue.Hold = new TaskCompletionSource<bool>();

        fixture.Catalogue.Holds = call => call.Query == "rocket";

        var box = SearchBox(panel);

        box.Text = "rocket";
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        Settle();
        ScrollToEnd(panel);

        Assert.All(fixture.Catalogue.Calls.Skip(1), call => Assert.Equal(new Call("rocket", new VideoFilters(), 1), call));

        hold.SetResult(true);
        Settle();

        Assert.Equal(25, RowCount(panel));
        Assert.Equal(new Call("rocket", new VideoFilters(), 1), fixture.Catalogue.Calls[^1]);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void ASearchWaitsAboveTheListWhileTheOldRowsStayDrawn()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture);
        var hold = fixture.Catalogue.Hold = new TaskCompletionSource<bool>();

        fixture.Catalogue.Holds = call => call.Query == "rocket";

        var box = SearchBox(panel);

        box.Text = "rocket";
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        Settle();
        Task.Delay(Waiting.Delay * 3).Wait();
        Settle();

        var waiting = panel.GetVisualDescendants().OfType<StatusLine>().Where(line => line.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == CataloguePage.Searching)).ToList();

        Assert.Single(waiting);
        Assert.DoesNotContain(waiting[0], ListScroll(panel).GetVisualDescendants());

        hold.SetResult(true);
        Settle();

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void ASearchTypedOnTheWatchPageFillsTheCataloguesSearchBox()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = Open(fixture);

        Assert.True(panel.GetVisualDescendants().OfType<WatchPage>().First().Submit("rocket"));
        Settle();

        Assert.Equal("rocket", SearchBox(panel).Text);

        window.Close();
        fixture.Dispose();
    }

    [AvaloniaFact]
    public void TheHeadsetCataloguePagesByTwentyFiveWithAPagerAndNoScrollingLoad()
    {
        var fixture = new Fixture(Many);
        var (window, panel) = OpenCatalogue(fixture, headset: true);

        Assert.Equal(25, RowCount(panel));
        Assert.Contains(Drawn(panel), text => text == "1–25 of 40");
        Assert.Contains(Drawn(panel), text => text == "40 videos");
        Assert.False(Named(panel, CataloguePage.PreviousName).IsEnabled);
        Assert.True(Named(panel, CataloguePage.NextName).IsEnabled);
        Assert.True(SearchBox(panel).IsEffectivelyVisible);

        Click(Named(panel, CataloguePage.NextName));

        Assert.Equal(2, fixture.Catalogue.Calls[^1].Page);
        Assert.Equal(15, RowCount(panel));
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
