using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each Search table takes the COLUMNS control and keeps what it stores (#2518).</summary>
public sealed class TheSearchTablesAreArrangedTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] SystemColumns = ["System", "Distance", "Allegiance", "Economy", "Population", "Power", "Reported"];

    private static readonly string[] StationColumns = ["Station", "System", "Distance", "Pad", "FromStar", "AskedFor", "Updated"];

    private static readonly string[] BodyColumns = ["Body", "Type", "System", "Distance", "FromStar", "Landable", "Gravity"];

    private static readonly string[] OutfittingColumns = ["Station", "System", "Distance", "Pad", "FromStar", "Updated"];

    private static readonly string[] SignalColumns = ["Signal", "System", "Distance", "Seen", "Source"];

    private static readonly string[] RareColumns = ["Name", "Station", "FromStar", "Distance", "Supply", "Price", "Profit", "Total", "Reported", "RareRun"];

    private static readonly SystemSummary[] Systems =
    [
        new() { Name = "Alioth", Distance = 3, Population = 1_000 },
        new() { Name = "Sol", Distance = 9, Population = 22_000_000_000 },
    ];

    private sealed class Quiet : StubStarSystemService
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
        public List<GalaxyQuery> Asked { get; } = [];

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            return Task.FromResult(new GalaxySearchResult("LTT 7786", 2, Systems));
        }
    }

    private sealed class World : GalaxySearchWorld
    {
        public World()
            : base(new Quiet()) =>
            Registry = CapabilityRegistry.Build([GalaxyCapability.Create(null, () => "LTT 7786", Settings, searches: Board)]);
    }

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Keys(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader).Layout.Columns.Select(column => column.Key)];

    private static GalaxySearchPosting Post(GalaxySearchKind kind, object result, params (string Key, string Value)[] arguments) =>
        new(kind, arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), "LTT 7786", result, "Matched.", At);

    private static (PanelView Panel, Window Window) Shown(World world, string root, ColumnArrangementMemory memory, bool headset) =>
        SearchPanelHosting.Show(world.Surface, root, headset, arrangements: memory);

    private static (PanelView Panel, Window Window) Systems_(ViewStateStore views, bool headset, Galaxy galaxy)
    {
        var (panel, window) = SearchPanelHosting.ShowSearchForm(
            TestSurface.Settings(),
            galaxy,
            new GalaxySearchBoard(),
            new Quiet(),
            SystemSearchPage.RootKey,
            new ColumnArrangementMemory(views, headset),
            headset);

        return (panel, window);
    }

    private static async Task Ask(PanelView panel)
    {
        await panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(new Dictionary<string, string> { ["allegiance"] = "Federation" });
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<(string Root, string Id, string[] Columns, GalaxySearchPosting Posting)> Tables(bool headset)
    {
        string[] Without(string[] columns, string key) => headset ? [.. columns.Where(column => column != key)] : columns;

        yield return (
            SystemSearchPage.RootKey,
            SystemSearchPage.TableId,
            Without(SystemColumns, "Reported"),
            Post(GalaxySearchKind.Systems, new GalaxySearchResult("LTT 7786", 2, Systems)));

        yield return (
            StationSearchPage.RootKey,
            StationSearchPage.TableId,
            Without(StationColumns, "Updated"),
            Post(GalaxySearchKind.Stations, new StationSearchResult("LTT 7786", 1, [new StationSummary { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra", Distance = 4.2 }])));

        yield return (
            BodySearchPage.RootKey,
            BodySearchPage.TableId,
            Without(BodyColumns, "Gravity"),
            Post(GalaxySearchKind.Bodies, new BodySearchResult("LTT 7786", 1, [new BodySummary { Name = "Alioth 1", SystemName = "Alioth", Distance = 9.9 }])));

        yield return (
            OutfittingSearchPage.RootKey,
            OutfittingSearchPage.TableId,
            Without(OutfittingColumns, "Updated"),
            Post(GalaxySearchKind.Outfitting, new StationSearchResult("LTT 7786", 1, [new StationSummary { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra", Distance = 4.2 }])));

        yield return (
            SignalSearchPage.RootKey,
            SignalSearchPage.TableId,
            SignalColumns,
            Post(GalaxySearchKind.Signals, new SignalSearchResult("LTT 7786", 1, [new SignalFinding("Notable stellar phenomena", "Alioth", 9.9, At, true)])));

        yield return (
            RareGoodsPage.RootKey,
            RareGoodsPage.TableId,
            RareColumns,
            Post(GalaxySearchKind.Rares, new RareSearchResult([new RareListingRow(RareCatalogue.All.First(), null, 12.5, 4_000)], false)));
    }

    public static TheoryData<string, bool> EveryTable()
    {
        var data = new TheoryData<string, bool>();

        foreach (var headset in new[] { false, true })
        {
            foreach (var table in Tables(headset))
            {
                data.Add(table.Root, headset);
            }
        }

        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void WithNothingStoredEachTableDrawsItsOwnColumns(string root, bool headset)
    {
        var table = Tables(headset).First(known => known.Root == root);
        var world = new World();
        world.Board.Post(table.Posting);

        var (panel, window) = Shown(world, root, new ColumnArrangementMemory(Views(), headset), headset);

        Assert.Equal(table.Columns, Keys(panel, table.Id));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void EachTableOffersAColumnsTileAfterTheOthersOnItsHead(string root, bool headset)
    {
        var table = Tables(headset).First(known => known.Root == root);
        var world = new World();
        world.Board.Post(table.Posting);

        var (panel, window) = Shown(world, root, new ColumnArrangementMemory(Views(), headset), headset);

        Assert.NotNull(Named<Button>(panel, ColumnArranger.NameOf(table.Id, "Tile")));

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EveryTable))]
    public void EachTableOffersAFilterWhoseDistanceColumnIsDisabled(string root, bool headset)
    {
        var table = Tables(headset).First(known => known.Root == root);
        var world = new World();
        world.Board.Post(table.Posting);

        var (panel, window) = Shown(world, root, new ColumnArrangementMemory(Views(), headset), headset);

        Press(panel, ColumnFilter.NameOf(table.Id, "Tile"));

        var cells = panel.GetVisualDescendants().OfType<Button>().Where(button => button.Name?.Contains("Head.Filter.", StringComparison.Ordinal) == true).ToList();
        Assert.False(cells.Single(cell => cell.Name!.EndsWith(".Filter.Distance", StringComparison.Ordinal)).IsEnabled);
        Assert.Contains(cells, cell => cell.IsEnabled);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAddedMovedAndRemovedColumnIsKeptWhenThePageOpensAgain(bool headset)
    {
        var views = Views();
        var table = Tables(headset).First(known => known.Root == StationSearchPage.RootKey);
        var world = new World();
        world.Board.Post(table.Posting);

        var (first, firstWindow) = Shown(world, table.Root, new ColumnArrangementMemory(views, headset), headset);

        Press(first, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(first, "StationHead.Choose.Pad");
        Press(first, ColumnArranger.NameOf(table.Id, "Remove"));
        Press(first, "StationHead.Choose.FromStar");
        Press(first, ColumnArranger.NameOf(table.Id, "MoveLeft"));
        Press(first, ColumnArranger.NameOf(table.Id, "Add"));
        Named<CheckBox>(first, ColumnArranger.NameOf(table.Id, "Show.PowerState")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Press(first, ColumnArranger.NameOf(table.Id, "Done"));

        var arranged = Keys(first, table.Id);
        Assert.DoesNotContain("Pad", arranged);
        Assert.Contains("PowerState", arranged);
        Assert.True(Array.IndexOf(arranged, "FromStar") < Array.IndexOf(arranged, "Distance"));
        firstWindow.Close();

        var (again, againWindow) = Shown(world, table.Root, new ColumnArrangementMemory(views, headset), headset);
        Assert.Equal(arranged, Keys(again, table.Id));

        Press(again, ColumnArranger.NameOf(table.Id, "Tile"));
        Press(again, ColumnArranger.NameOf(table.Id, "Default"));
        Assert.Equal(table.Columns, Keys(again, table.Id));
        againWindow.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASortFromAHeaderFollowsItsColumnWhenItMovesAndAsksAgainForNothing(bool headset)
    {
        var galaxy = new Galaxy();
        var (panel, window) = Systems_(Views(), headset, galaxy);

        await Ask(panel);
        Press(panel, "SystemHead.Population");
        Assert.Equal("population", galaxy.Asked[^1].Sort!.Field.Name);
        var asked = galaxy.Asked.Count;

        Press(panel, ColumnArranger.NameOf(SystemSearchPage.TableId, "Tile"));
        Press(panel, "SystemHead.Choose.Population");
        Press(panel, ColumnArranger.NameOf(SystemSearchPage.TableId, "MoveLeft"));
        Press(panel, ColumnArranger.NameOf(SystemSearchPage.TableId, "Done"));

        var keys = Keys(panel, SystemSearchPage.TableId);
        Assert.True(Array.IndexOf(keys, "Population") < Array.IndexOf(keys, "Economy"));
        Assert.Equal("· MOST POPULOUS", panel.GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == GalaxySearchPage.SortedName).Text);
        Assert.Contains("▼", Named<Button>(panel, "SystemHead.Population").GetVisualDescendants().OfType<TextBlock>().First().Text);
        Assert.Equal(asked, galaxy.Asked.Count);

        Press(panel, ColumnArranger.NameOf(SystemSearchPage.TableId, "Tile"));
        Press(panel, "SystemHead.Choose.Population");
        Press(panel, ColumnArranger.NameOf(SystemSearchPage.TableId, "Remove"));
        Press(panel, ColumnArranger.NameOf(SystemSearchPage.TableId, "Done"));

        Assert.DoesNotContain("Population", Keys(panel, SystemSearchPage.TableId));
        Assert.Equal(asked, galaxy.Asked.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void AnAskedForMaterialsColumnCannotBeRemovedAndOtherwiseThereIsNone()
    {
        var bodies = new[] { new BodySummary { Name = "Alioth 1", SystemName = "Alioth", Distance = 9.9, Materials = [("Polonium", 1.1)] } };
        var world = new World();
        world.Board.Post(Post(GalaxySearchKind.Bodies, new BodySearchResult("LTT 7786", 1, bodies), ("material", "Polonium"), ("order_by", "material")));

        var (panel, window) = Shown(world, BodySearchPage.RootKey, new ColumnArrangementMemory(Views(), false), false);

        Assert.Contains("Share", Keys(panel, BodySearchPage.TableId));

        Press(panel, ColumnArranger.NameOf(BodySearchPage.TableId, "Tile"));
        Press(panel, "BodyHead.Choose.Share");
        Assert.False(Named<Button>(panel, ColumnArranger.NameOf(BodySearchPage.TableId, "Remove")).IsEnabled);

        window.Close();

        var plain = new World();
        plain.Board.Post(Post(GalaxySearchKind.Bodies, new BodySearchResult("LTT 7786", 1, bodies)));
        var (without, withoutWindow) = Shown(plain, BodySearchPage.RootKey, new ColumnArrangementMemory(Views(), false), false);

        Assert.DoesNotContain("Share", Keys(without, BodySearchPage.TableId));
        withoutWindow.Close();
    }

    [AvaloniaFact]
    public void AnAddedFlagColumnReadsYesOrNo()
    {
        var bodies = new[] { new BodySummary { Name = "Alioth 1", SystemName = "Alioth", Distance = 9.9, IsTidallyLocked = true } };
        var world = new World();
        world.Board.Post(Post(GalaxySearchKind.Bodies, new BodySearchResult("LTT 7786", 1, bodies)));

        var (panel, window) = Shown(world, BodySearchPage.RootKey, new ColumnArrangementMemory(Views(), false), false);

        Press(panel, ColumnArranger.NameOf(BodySearchPage.TableId, "Tile"));
        Press(panel, ColumnArranger.NameOf(BodySearchPage.TableId, "Add"));
        Named<CheckBox>(panel, ColumnArranger.NameOf(BodySearchPage.TableId, "Show.TidallyLocked")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "Yes" && block.GetVisualAncestors().OfType<TableGrid>().Any(grid => !grid.IsHeader));

        window.Close();
    }

    [AvaloniaFact]
    public void RareRunStaysLastWhenAColumnIsAdded()
    {
        var world = new World();
        world.Board.Post(Tables(false).First(known => known.Root == RareGoodsPage.RootKey).Posting);

        var (panel, window) = Shown(world, RareGoodsPage.RootKey, new ColumnArrangementMemory(Views(), false), false);

        Press(panel, ColumnArranger.NameOf(RareGoodsPage.TableId, "Tile"));
        Press(panel, ColumnArranger.NameOf(RareGoodsPage.TableId, "Add"));
        Named<CheckBox>(panel, ColumnArranger.NameOf(RareGoodsPage.TableId, "Show.SellsFor")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var keys = Keys(panel, RareGoodsPage.TableId);
        Assert.Equal("RareRun", keys[^1]);
        Assert.Equal("SellsFor", keys[^2]);

        window.Close();
    }
}
