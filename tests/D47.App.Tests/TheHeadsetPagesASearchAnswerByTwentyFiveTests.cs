using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The headset shows a search answer 25 rows to a page, with PREVIOUS and NEXT over the rows fetched (#2637).</summary>
public sealed class TheHeadsetPagesASearchAnswerByTwentyFiveTests
{
    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy(int total) : StubGalaxyService
    {
        public List<GalaxyQuery> Asked { get; } = [];

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            var first = ((query.Page - 1) * query.Size) + 1;
            var rows = Enumerable.Range(first, Math.Clamp(total - first + 1, 0, query.Size))
                .Select(n => new SystemSummary { Name = $"System {n}", Distance = n, PrimaryEconomy = "Industrial" })
                .ToList();

            return Task.FromResult(new GalaxySearchResult("LTT 7786", total, rows) { More = query.Page * query.Size < total });
        }
    }

    private static readonly Dictionary<string, string> Anything = new() { ["near"] = "LTT 7786" };

    private static (PanelView Panel, Window Window) Shown(Galaxy galaxy, bool headset)
    {
        var views = new ViewStateStore(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);
        var arrangements = new ColumnArrangementMemory(views, headset);
        var (panel, window) = SearchPanelHosting.ShowSearchForm(TestSurface.Settings(), galaxy, new GalaxySearchBoard(), new Systems(), SystemSearchPage.RootKey, arrangements, headset);
        Settle();

        return (panel, window);
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task Ask(Control root)
    {
        await root.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(Anything);
        Settle();
    }

    private static Button? Find(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name);

    private static void Press(Control root, string name)
    {
        var button = Find(root, name)!;
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle();
    }

    private static List<string> Names(Control root) =>
        [.. root.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName)
            .Items.OfType<SystemSummary>().Select(system => system.Name)];

    private static string? Readout(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Name == GalaxySearchPage.FilterEndName)?.Text;

    private static List<string> Range(int from, int to) => [.. Enumerable.Range(from, to - from + 1).Select(n => $"System {n}")];

    [AvaloniaFact]
    public async Task PreviousAndNextMoveThroughTheRowsFetchedAndAskTheServiceNothing()
    {
        var galaxy = new Galaxy(60);
        var (panel, window) = Shown(galaxy, headset: true);

        await Ask(panel);
        Assert.Equal(Range(1, 25), Names(panel));

        Press(panel, GalaxySearchPage.FetchNextName);

        Assert.Equal(2, galaxy.Asked.Count);
        Assert.Equal(Range(26, 50), Names(panel));
        Assert.Equal("26–50 of 50", Readout(panel));
        Assert.True(Find(panel, GalaxySearchPage.PreviousPageName)!.IsEffectivelyEnabled);
        Assert.False(Find(panel, GalaxySearchPage.NextPageName)!.IsEffectivelyEnabled);

        Press(panel, GalaxySearchPage.PreviousPageName);
        Assert.Equal(Range(1, 25), Names(panel));
        Assert.Equal("1–25 of 50", Readout(panel));
        Assert.False(Find(panel, GalaxySearchPage.PreviousPageName)!.IsEffectivelyEnabled);

        Press(panel, GalaxySearchPage.NextPageName);
        Assert.Equal(Range(26, 50), Names(panel));
        Assert.Equal(2, galaxy.Asked.Count);

        window.Close();
    }

    [AvaloniaFact]
    public async Task FetchingTheLastPageMovesToItAndEndsTheFetchTile()
    {
        var galaxy = new Galaxy(60);
        var (panel, window) = Shown(galaxy, headset: true);

        await Ask(panel);
        Press(panel, GalaxySearchPage.FetchNextName);
        Press(panel, GalaxySearchPage.FetchNextName);

        Assert.Equal(3, galaxy.Asked.Count);
        Assert.Equal(Range(51, 60), Names(panel));
        Assert.Equal("51–60 of 60", Readout(panel));
        Assert.Null(Find(panel, GalaxySearchPage.FetchNextName));
        Assert.False(Find(panel, GalaxySearchPage.NextPageName)!.IsEffectivelyEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ANewSearchReturnsToTheFirstPage()
    {
        var galaxy = new Galaxy(60);
        var (panel, window) = Shown(galaxy, headset: true);

        await Ask(panel);
        Press(panel, GalaxySearchPage.FetchNextName);
        Assert.Equal(Range(26, 50), Names(panel));

        await Ask(panel);

        Assert.Equal(Range(1, 25), Names(panel));
        Assert.Equal("1–25 of 25", Readout(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task AnAnswerOfTwentyFiveWithNothingMoreHasNoPager()
    {
        var galaxy = new Galaxy(25);
        var (panel, window) = Shown(galaxy, headset: true);

        await Ask(panel);

        Assert.Equal(Range(1, 25), Names(panel));
        Assert.Null(Readout(panel));
        Assert.Null(Find(panel, GalaxySearchPage.NextPageName));
        Assert.Null(Find(panel, GalaxySearchPage.FetchNextName));

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheDesktopDrawsEveryRowFetchedWithoutAPager()
    {
        var galaxy = new Galaxy(60);
        var (panel, window) = Shown(galaxy, headset: false);

        await Ask(panel);
        Assert.Null(Find(panel, GalaxySearchPage.NextPageName));
        Assert.Equal(Range(1, 25), Names(panel));

        window.Close();
    }
}
