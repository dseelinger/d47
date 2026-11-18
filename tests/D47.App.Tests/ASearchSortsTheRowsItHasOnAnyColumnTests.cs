using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A Search column the service cannot order by sorts the rows already fetched, says so, and the sort is kept (#2576).</summary>
public sealed class ASearchSortsTheRowsItHasOnAnyColumnTests
{
    private static readonly DateTimeOffset AskedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly BodySearchResult ThreeBodies = new(
        "LTT 7786",
        3,
        [
            new BodySummary { Name = "Near One", SystemName = "Sol", Distance = 1, Volcanism = "Water Geysers" },
            new BodySummary { Name = "Mid Two", SystemName = "Sol", Distance = 2, Volcanism = "Ammonia Magma" },
            new BodySummary { Name = "Far Three", SystemName = "Sol", Distance = 3, Volcanism = "Silicate Vapour Geysers" },
        ]);

    private static readonly string[] ByVolcanism = ["Mid Two", "Far Three", "Near One"];

    private static readonly string[] Nearest = ["Near One", "Mid Two", "Far Three"];

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

    private sealed class Galaxy : StubGalaxyService
    {
        public List<BodyQuery> Bodies { get; } = [];

        public List<GalaxyQuery> Systems { get; } = [];

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Bodies.Add(query);
            return Task.FromResult(ThreeBodies);
        }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Systems.Add(query);

            return Task.FromResult(new GalaxySearchResult(
                "LTT 7786",
                2,
                [
                    new SystemSummary { Name = "Alioth", Distance = 3, Population = 1_000 },
                    new SystemSummary { Name = "Sol", Distance = 9, Population = 22_000_000_000 },
                ]));
        }
    }

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static (PanelView Panel, Window Window, GalaxySearchBoard Board) Shown(Galaxy galaxy, string root, ViewStateStore views, GalaxySearchBoard? board = null)
    {
        board ??= new GalaxySearchBoard();
        var arrangements = new ColumnArrangementMemory(views);

        if (root == BodySearchPage.RootKey && arrangements.Remembered(BodySearchPage.TableId) is null)
        {
            arrangements.Remember(BodySearchPage.TableId, ["Body", "System", "Distance", "FromStar", "Landable", "Volcanism"]);
        }

        var (panel, window) = SearchPanelHosting.ShowSearchForm(TestSurface.Settings(), galaxy, board, new Systems(), root, arrangements);

        return (panel, window, board);
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string? Words(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Name == GalaxySearchPage.SortedName)?.Text;

    private static async Task Ask(PanelView panel, IReadOnlyDictionary<string, string> arguments)
    {
        await panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(arguments);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Order(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Select(text => Nearest.FirstOrDefault(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()];

    private static readonly Dictionary<string, string> Landable = new() { ["landable"] = "true" };

    private static GalaxySearchPosting Voice(IReadOnlyDictionary<string, string> arguments) =>
        new(GalaxySearchKind.Bodies, arguments, "LTT 7786", ThreeBodies, "Bodies matched.", AskedAt);

    [AvaloniaFact]
    public async Task PressingVolcanismSortsTheRowsFetchedWithoutAskingTheServiceAgain()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy, BodySearchPage.RootKey, Views());

        await Ask(panel, Landable);
        Assert.Equal(Nearest, Order(panel));
        Assert.Single(galaxy.Bodies);

        panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single();
        Press(Named(panel, "BodyHead.Volcanism"));

        Assert.Single(galaxy.Bodies);
        Assert.Equal(ByVolcanism, Order(panel));
        Assert.Equal("· THESE 3 SORTED BY VOLCANISM", Words(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task PressingDistanceAfterALocalSortAsksTheServiceAgainAndClearsTheLocalSort()
    {
        var galaxy = new Galaxy();
        var views = Views();
        var (panel, window, _) = Shown(galaxy, BodySearchPage.RootKey, views);

        await Ask(panel, Landable);
        Press(Named(panel, "BodyHead.Volcanism"));
        Press(Named(panel, "BodyHead.LY"));

        Assert.Equal(2, galaxy.Bodies.Count);
        Assert.Equal("distance", galaxy.Bodies[^1].Sort!.Field.Name);
        Assert.Equal(Nearest, Order(panel));
        Assert.Null(Words(panel));
        Assert.Equal("distance", new ColumnSortMemory(views).RememberedOrderBy(BodySearchPage.TableId));

        window.Close();
    }

    [AvaloniaFact]
    public async Task APressAgainOnALocalSortReversesItAndAThirdReturnsTheFetchedOrder()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy, BodySearchPage.RootKey, Views());

        await Ask(panel, Landable);
        Press(Named(panel, "BodyHead.Volcanism"));
        Press(Named(panel, "BodyHead.Volcanism"));

        Assert.Equal(ByVolcanism.Reverse(), Order(panel));

        Press(Named(panel, "BodyHead.Volcanism"));

        Assert.Equal(Nearest, Order(panel));
        Assert.Single(galaxy.Bodies);
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text?.Contains("THESE", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public async Task RowsThatArriveByScrollingAreSortedWithTheRowsHeldAndTheHeadCountsThemAll()
    {
        var galaxy = new Galaxy();
        var (panel, window, board) = Shown(galaxy, BodySearchPage.RootKey, Views());

        var first = Voice(Landable) with
        {
            Result = new BodySearchResult("LTT 7786", 4, [.. ThreeBodies.Bodies.Take(2)]),
        };

        board.Post(first);
        Dispatcher.UIThread.RunJobs();
        Press(Named(panel, "BodyHead.Volcanism"));

        // Pressed on a posting the Commander did not ask from the page, the sort still covers that answer.
        Assert.Equal("· THESE 2 SORTED BY VOLCANISM", Words(panel));

        var next = first with { Result = new BodySearchResult("LTT 7786", 4, [ThreeBodies.Bodies[2]]), Pages = 2, More = false };
        Assert.True(board.Extend(next));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ByVolcanism, Order(panel));
        Assert.Equal("· THESE 3 SORTED BY VOLCANISM", Words(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ARemoteSortIsSentAsOrderByByASearchRunFromTheFormOnANewPageOnTheSameStore()
    {
        var views = Views();
        var firstGalaxy = new Galaxy();
        var (first, firstWindow, _) = Shown(firstGalaxy, SystemSearchPage.RootKey, views);

        await Ask(first, new Dictionary<string, string> { ["allegiance"] = "Federation" });
        Press(Named(first, "SystemHead.Population"));
        firstWindow.Close();

        var galaxy = new Galaxy();
        var (again, window, _) = Shown(galaxy, SystemSearchPage.RootKey, views);

        Press(Named(again, GalaxySearchPage.RunName));
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("population", Assert.Single(galaxy.Systems).Sort!.Field.Name);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ALocalSortIsAppliedToTheAnswerOfASearchRunFromTheFormOnANewPageOnTheSameStore()
    {
        var views = Views();
        var (first, firstWindow, _) = Shown(new Galaxy(), BodySearchPage.RootKey, views);

        await Ask(first, Landable);
        Press(Named(first, "BodyHead.Volcanism"));
        firstWindow.Close();

        var galaxy = new Galaxy();
        var (again, window, _) = Shown(galaxy, BodySearchPage.RootKey, views);

        again.GetVisualDescendants().OfType<GalaxySearchPage>().Single().Run(Landable);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(galaxy.Bodies);
        Assert.Null(galaxy.Bodies[0].Sort);
        Assert.Equal(ByVolcanism, Order(again));
        Assert.Equal("· THESE 3 SORTED BY VOLCANISM", Words(again));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ASearchPostedByVoiceKeepsItsOrderAndLeavesTheRememberedSortAlone()
    {
        var views = Views();
        var (panel, window, board) = Shown(new Galaxy(), BodySearchPage.RootKey, views);

        await Ask(panel, Landable);
        Press(Named(panel, "BodyHead.Volcanism"));

        board.Post(Voice(new Dictionary<string, string>(StringComparer.Ordinal) { ["landable"] = "true", ["max_distance"] = "50" }));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Nearest, Order(panel));
        Assert.Null(Words(panel));

        var kept = new ColumnSortMemory(views).Remembered(BodySearchPage.TableId);
        Assert.Equal("Volcanism", kept?.Key);

        window.Close();
    }

    [AvaloniaFact]
    public async Task EveryHeaderOnTheSignalAndOutfittingPagesSorts()
    {
        foreach (var (root, head, labels) in new (string, string, string[])[]
                 {
                     (SignalSearchPage.RootKey, SignalSearchPage.HeadName, ["Signal", "System", "LY", "Seen", "Source"]),
                     (OutfittingSearchPage.RootKey, OutfittingSearchPage.HeadName, ["Station", "System", "LY", "Pad", "Fromstar"]),
                 })
        {
            var galaxy = new Galaxy();
            var (panel, window, board) = Shown(galaxy, root, Views());
            var kind = root == SignalSearchPage.RootKey ? GalaxySearchKind.Signals : GalaxySearchKind.Outfitting;

            if (kind == GalaxySearchKind.Outfitting)
            {
                board.Post(new GalaxySearchPosting(
                    kind,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    "LTT 7786",
                    new StationSearchResult("LTT 7786", 1, [new StationSummary { Name = "Daedalus", SystemName = "Sol", Distance = 40 }]),
                    "Matched.",
                    AskedAt));
            }
            else
            {
                board.Post(new GalaxySearchPosting(
                    kind,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    "LTT 7786",
                    new SignalSearchResult("LTT 7786", 1, [new SignalFinding("Notable stellar phenomena", "Sol", 4, AskedAt, false)]),
                    "Matched.",
                    AskedAt));
            }

            Dispatcher.UIThread.RunJobs();

            var buttons = panel.GetVisualDescendants().OfType<Button>().Select(button => button.Name).ToList();

            foreach (var label in labels)
            {
                Assert.Contains($"{head}.{label}", buttons);
            }

            window.Close();
        }
    }
}
