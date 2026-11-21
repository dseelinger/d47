using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>FILTER on a search answer hides rows already fetched, filters each later page, and asks the service nothing (#2632).</summary>
public sealed class ASearchAnswerHidesRowsByTheValuesTickedTests
{
    private static readonly string[] Economies = ["Industrial", "High Tech", "Refinery", "Agriculture", "Extraction"];

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

    /// <summary>Answers pages of 25 from 60 systems; system 51 and on are Military, a value first seen on page three.</summary>
    private sealed class Galaxy : StubGalaxyService
    {
        public List<GalaxyQuery> Asked { get; } = [];

        public List<BodyQuery> Bodies { get; } = [];

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            const int total = 60;
            var first = ((query.Page - 1) * query.Size) + 1;
            var rows = Enumerable.Range(first, Math.Clamp(total - first + 1, 0, query.Size))
                .Select(n => new SystemSummary
                {
                    Name = $"System {n}",
                    Distance = n,
                    PrimaryEconomy = n > 50 ? "Military" : Economies[n % Economies.Length],
                    Security = n % 4 == 0 ? null : "High",
                    ControllingPower = n % 3 == 0 ? "Jerome Archer" : null,
                })
                .ToList();

            return Task.FromResult(new GalaxySearchResult("LTT 7786", total, rows) { More = query.Page * query.Size < total });
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Bodies.Add(query);

            return Task.FromResult(new BodySearchResult(
                "LTT 7786",
                3,
                [
                    new BodySummary { Name = "Near One", SystemName = "Sol", Distance = 1, Subtype = "Icy body", Volcanism = "Water Geysers" },
                    new BodySummary { Name = "Mid Two", SystemName = "Sol", Distance = 2, Subtype = "Rocky body", Volcanism = "Ammonia Magma" },
                    new BodySummary { Name = "Far Three", SystemName = "Sol", Distance = 3, Subtype = "Rocky body", Volcanism = "Silicate Vapour Geysers" },
                ]));
        }
    }

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static (PanelView Panel, Window Window, GalaxySearchBoard Board) Shown(Galaxy galaxy, string root = SystemSearchPage.RootKey, bool headset = false)
    {
        var board = new GalaxySearchBoard();
        var arrangements = new ColumnArrangementMemory(Views(), headset);

        if (root == BodySearchPage.RootKey)
        {
            arrangements.Remember(BodySearchPage.TableId, ["Body", "Type", "System", "Distance", "Volcanism"]);
        }

        var (panel, window) = SearchPanelHosting.ShowSearchForm(TestSurface.Settings(), galaxy, board, new Systems(), root, arrangements, headset);
        Settle();

        return (panel, window, board);
    }

    /// <summary>Makes the window tall enough that the rows' end, realised only as it scrolls into view, is drawn unscrolled.</summary>
    private static void Tall(Window window)
    {
        window.Height = 2400;
        Settle();
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task Ask(Control root, IReadOnlyDictionary<string, string> arguments)
    {
        await root.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(arguments);
        Settle();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static bool Has(Control root, string name) =>
        root.GetVisualDescendants().OfType<Control>().Any(control => control.Name == name && control.IsEffectivelyVisible);

    private static void Press(Control root, string name)
    {
        var button = Named(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle();
    }

    private static CheckBox Value(Control root, string table, string key, int block, string value) =>
        root.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == ColumnFilter.ValueName(table, key, block, value));

    /// <summary>Opens the filter bar, chooses the field, sets the value, and closes the bar.</summary>
    private static void Set(Control root, string table, string head, string key, int block, string value, bool ticked)
    {
        Press(root, ColumnFilter.NameOf(table, "Tile"));
        Press(root, $"{head}.Filter.{key}");
        Value(root, table, key, block, value).IsChecked = ticked;
        Settle();
        Press(root, ColumnFilter.NameOf(table, "Done"));
    }

    private static List<string> Names(Control root) =>
        [.. root.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName)
            .Items.OfType<SystemSummary>().Select(system => system.Name)];

    private static List<string> BodyNames(Control root) =>
        [.. root.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName)
            .Items.OfType<BodySummary>().Select(body => body.Name)];

    private static string? Words(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Name == GalaxySearchPage.SortedName && block.IsEffectivelyVisible)?.Text;

    private static string? Text(Control root, string name) =>
        root.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Name == name && block.IsEffectivelyVisible)?.Text;

    private static List<string> Economy(int from, int to) =>
        [.. Enumerable.Range(from, to - from + 1).Where(n => Economies[n % Economies.Length] == "Industrial").Select(n => $"System {n}")];

    private static readonly Dictionary<string, string> Anything = new() { ["near"] = "LTT 7786" };

    [AvaloniaFact]
    public async Task EachSearchAnswerOffersFilterWithItsNumberColumnsDisabledAndItsValuesListed()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy);

        await Ask(panel, Anything);
        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Tile"));

        Assert.False(Named(panel, "SystemHead.Filter.Distance").IsEnabled);
        Assert.False(Named(panel, "SystemHead.Filter.Population").IsEnabled);
        Assert.False(Named(panel, "SystemHead.Filter.Reported").IsEnabled);
        Assert.True(Named(panel, "SystemHead.Filter.Economy").IsEnabled);
        Assert.True(Named(panel, "SystemHead.Filter.System").IsEnabled);

        Press(panel, "SystemHead.Filter.Economy");

        foreach (var economy in Economies)
        {
            Assert.True(Value(panel, SystemSearchPage.TableId, "Economy", 0, economy).IsChecked);
        }

        Assert.True(Value(panel, SystemSearchPage.TableId, "Economy", 1, "High").IsChecked);
        Assert.True(Value(panel, SystemSearchPage.TableId, "Economy", 1, "No security reported").IsChecked);

        Press(panel, "SystemHead.Filter.Power");
        Assert.True(Value(panel, SystemSearchPage.TableId, "Power", 0, "No Power reported").IsChecked);
        Assert.Equal(
            "These hide rows D47 already has. Nothing is sent to Spansh, and the spoken answer still covers all 25.",
            Text(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Kept")));

        window.Close();
    }

    [AvaloniaFact]
    public async Task UntickingAValueHidesExactlyItsRowsAndTheHeadSaysSo()
    {
        var galaxy = new Galaxy();
        var (panel, window, board) = Shown(galaxy);

        await Ask(panel, Anything);
        var answer = board.Last(GalaxySearchKind.Systems)!.Answer;

        Set(panel, SystemSearchPage.TableId, SystemSearchPage.HeadName, "Economy", 0, "Industrial", ticked: false);

        var industrial = Economy(1, 25);
        Assert.Equal([.. Enumerable.Range(1, 25).Select(n => $"System {n}").Except(industrial)], Names(panel));
        Assert.Equal($"THESE {25 - industrial.Count} OF 25, FILTERED BY ECONOMY", Text(panel, GalaxySearchPage.FilterWordsName));
        Assert.Null(Words(panel));
        Assert.True(Has(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "HeadClear")));
        Assert.Equal("Filter · 1", Named(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Tile")).Content);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-filter-head.png");
        }

        Assert.Single(galaxy.Asked);
        Assert.Equal(answer, board.Last(GalaxySearchKind.Systems)!.Answer);

        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "HeadClear"));

        Assert.Equal(25, Names(panel).Count);
        Assert.False(Has(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "HeadClear")));

        window.Close();
    }

    [AvaloniaFact]
    public async Task AFilterLeavingFewRowsFetchesNothingUntilFetchTheNextIsPressed()
    {
        var galaxy = new Galaxy();
        var (panel, window, board) = Shown(galaxy);

        await Ask(panel, Anything);

        foreach (var economy in Economies.Where(economy => economy != "Industrial"))
        {
            Set(panel, SystemSearchPage.TableId, SystemSearchPage.HeadName, "Economy", 0, economy, ticked: false);
        }

        panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().Tick();
        Settle();

        Assert.Single(galaxy.Asked);
        Assert.Equal(Economy(1, 25), Names(panel));
        Assert.Equal($"Rows 1–25 fetched · {25 - Economy(1, 25).Count} hidden by the filter", Text(panel, GalaxySearchPage.FilterEndName));

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-filter-end-line.png");
        }

        Assert.Equal("FETCH THE NEXT 25", Named(panel, GalaxySearchPage.FetchNextName).Content);

        Press(panel, GalaxySearchPage.FetchNextName);

        Assert.Equal(2, galaxy.Asked.Count);
        Assert.Equal(2, galaxy.Asked[^1].Page);
        Assert.Equivalent(galaxy.Asked[0] with { Page = 2 }, galaxy.Asked[^1], strict: true);
        Assert.Equal(Economy(1, 50), Names(panel));

        Tall(window);
        Assert.Equal($"Rows 1–50 fetched · {50 - Economy(1, 50).Count} hidden by the filter", Text(panel, GalaxySearchPage.FilterEndName));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ScrollingToTheEndOfAFilteredListThatOverflowsFetchesTheNextPage()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy);

        await Ask(panel, Anything);
        Set(panel, SystemSearchPage.TableId, SystemSearchPage.HeadName, "Economy", 0, "Industrial", ticked: false);

        var scroll = panel.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName).FindAncestorOfType<ScrollViewer>()!;
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, $"the rows fill {scroll.Extent.Height} of {scroll.Viewport.Height}");
        Assert.Single(galaxy.Asked);

        scroll.Offset = new Avalonia.Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
        Settle();

        Assert.Equal(2, galaxy.Asked.Count);
        Assert.Equal(50 - Economy(1, 50).Count, Names(panel).Count);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AValueFirstSeenOnALaterPageIsAddedTickedAndItsRowsShow()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy);

        await Ask(panel, Anything);

        foreach (var economy in Economies.Where(economy => economy != "Industrial"))
        {
            Set(panel, SystemSearchPage.TableId, SystemSearchPage.HeadName, "Economy", 0, economy, ticked: false);
        }

        Press(panel, GalaxySearchPage.FetchNextName);
        Tall(window);
        Press(panel, GalaxySearchPage.FetchNextName);

        Assert.Equal(3, galaxy.Asked.Count);
        Assert.Equal([.. Economy(1, 50), .. Enumerable.Range(51, 10).Select(n => $"System {n}")], Names(panel));
        Assert.False(Has(panel, GalaxySearchPage.FetchNextName));

        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Tile"));
        Press(panel, "SystemHead.Filter.Economy");

        Assert.True(Value(panel, SystemSearchPage.TableId, "Economy", 0, "Military").IsChecked);
        Assert.False(Value(panel, SystemSearchPage.TableId, "Economy", 0, "Refinery").IsChecked);

        window.Close();
    }

    [AvaloniaFact]
    public async Task EveryRowHiddenShowsClearFiltersAndFetchTheNext()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy);

        await Ask(panel, Anything);
        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Tile"));
        Press(panel, "SystemHead.Filter.Economy");
        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "UntickAll"));
        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Done"));

        Assert.Empty(Names(panel));
        Assert.Equal("No row fetched has a ticked Economy.", Text(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "Empty")));
        Assert.True(Has(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "EmptyClear")));
        Assert.True(Has(panel, GalaxySearchPage.FetchNextName));
        Assert.Single(galaxy.Asked);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-filter-all-hidden.png");
        }

        Press(panel, ColumnFilter.NameOf(SystemSearchPage.TableId, "EmptyClear"));

        Assert.Equal(25, Names(panel).Count);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ANewSearchClearsTheFilterAndKeepsTheRememberedSort()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy, BodySearchPage.RootKey);

        await Ask(panel, new Dictionary<string, string> { ["landable"] = "true" });
        Press(panel, "BodyHead.Volcanism");
        Set(panel, BodySearchPage.TableId, BodySearchPage.HeadName, "Type", 0, "Icy body", ticked: false);

        Assert.Equal(["Mid Two", "Far Three"], BodyNames(panel));
        Assert.Equal("THESE 2 OF 3 SORTED BY VOLCANISM, FILTERED BY TYPE", Text(panel, GalaxySearchPage.FilterWordsName));
        Assert.Null(Words(panel));

        await Ask(panel, new Dictionary<string, string> { ["landable"] = "true" });

        Assert.Equal(["Mid Two", "Far Three", "Near One"], BodyNames(panel));
        Assert.Equal("· THESE 3 SORTED BY VOLCANISM", Words(panel));
        Assert.Null(Text(panel, GalaxySearchPage.FilterWordsName));
        Assert.Equal("Filter", Named(panel, ColumnFilter.NameOf(BodySearchPage.TableId, "Tile")).Content);
        Assert.Equal(2, galaxy.Bodies.Count);
        Assert.Equivalent(galaxy.Bodies[0], galaxy.Bodies[1], strict: true);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheHeadsetEndsTheRowsWithHowManyAreShown()
    {
        var galaxy = new Galaxy();
        var (panel, window, _) = Shown(galaxy, headset: true);

        await Ask(panel, Anything);
        Assert.Single(galaxy.Asked);
        Set(panel, SystemSearchPage.TableId, SystemSearchPage.HeadName, "Economy", 0, "Industrial", ticked: false);

        var shown = 25 - Economy(1, 25).Count;
        Assert.Equal(shown, Names(panel).Count);
        Assert.Equal($"1–{shown} of {shown} shown · 25 fetched", Text(panel, GalaxySearchPage.FilterEndName));

        window.Width = 1024;
        window.Height = 640;
        Settle();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("search-filter-headset.png");
        }

        window.Close();
    }
}
