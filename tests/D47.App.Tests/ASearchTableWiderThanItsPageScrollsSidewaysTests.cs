using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// On the desktop, a Search table drawn with its header above a scrolling list scrolls sideways when its columns do not
/// fit, header and rows together, and keeps its rows virtualised (#2566).
/// </summary>
public sealed class ASearchTableWiderThanItsPageScrollsSidewaysTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] EveryBodyColumn =
    [
        "Body", "System", "Distance", "FromStar", "Landable", "Gravity", "Atmosphere", "MappingValue", "Reserves", "Rings",
        "Signals", "SurfaceTemperature", "Terraforming", "TidallyLocked", "Volcanism",
    ];

    private static readonly string[] RareColumnsWithSellsFor =
        ["Name", "Station", "FromStar", "Distance", "Supply", "Price", "Profit", "Total", "Reported", "SellsFor"];

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

    private sealed class World : GalaxySearchWorld
    {
        public World()
            : base(new Quiet()) =>
            Registry = CapabilityRegistry.Build([GalaxyCapability.Create(null, () => "LTT 7786", Settings, searches: Board)]);
    }

    private static ColumnArrangementMemory Memory(bool headset = false) =>
        new(new ViewStateStore(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance), headset);

    private static GalaxySearchPosting Post(GalaxySearchKind kind, object result) =>
        new(kind, new Dictionary<string, string>(StringComparer.Ordinal), "LTT 7786", result, "Matched.", At);

    private static GalaxySearchPosting Bodies(int count) =>
        Post(
            GalaxySearchKind.Bodies,
            new BodySearchResult(
                "LTT 7786",
                count,
                [.. Enumerable.Range(1, count).Select(n => new BodySummary { Name = $"Alioth {n}", SystemName = "Alioth", Distance = n })]));

    private static (PanelView Panel, Window Window) Shown(GalaxySearchPosting posting, string root, ColumnArrangementMemory memory, double width, bool headset = false)
    {
        var world = new World();
        world.Board.Post(posting);

        var (panel, window) = SearchPanelHosting.Show(world.Surface, root, headset, arrangements: memory);
        window.Width = width;
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

    private static List<TableGrid> Grids(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == id)];

    private static ScrollViewer Box(TableGrid grid) => (ScrollViewer)grid.FindAncestorOfType<TableScroll>()!.Child!;

    private static ScrollViewer RowScroller(Visual root) =>
        root.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName).FindAncestorOfType<ScrollViewer>()!;

    private static double Left(Visual control, Visual window) => control.TranslatePoint(default, window)!.Value.X;

    private static void AssertScrollsTogether(Visual window, string id)
    {
        var grids = Grids(window, id);
        var header = grids.Single(grid => grid.IsHeader);
        var rows = grids.Where(grid => !grid.IsHeader).ToList();
        var box = Box(header);

        Assert.True(box.Extent.Width > box.Viewport.Width + 1, $"extent {box.Extent.Width}, viewport {box.Viewport.Width}");
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Same(box, Box(row)));
        Assert.All(rows, row => Assert.Equal(header.Bounds.Width, row.Bounds.Width, 1));

        for (var column = 0; column < header.Layout.Columns.Count; column++)
        {
            Assert.True(header.Layout.Widths[column] >= header.Layout.MinimumOf(column) - 0.5, header.Layout.Columns[column].Key);
        }

        var headerAt = Left(header, window);
        var rowAt = Left(rows[0], window);
        var end = box.Extent.Width - box.Viewport.Width;

        box.Offset = new Vector(end, 0);
        Settle();

        Assert.Equal(end, box.Offset.X, 1);
        Assert.Equal(headerAt - end, Left(header, window), 1);
        Assert.Equal(rowAt - end, Left(rows[0], window), 1);

        var headerRight = header.TranslatePoint(new Point(header.Bounds.Width, 0), window)!.Value.X;
        var boxRight = box.TranslatePoint(new Point(box.Viewport.Width, 0), window)!.Value.X;
        Assert.True(headerRight <= boxRight + 0.5, $"the last column ends at {headerRight}, the box at {boxRight}");
    }

    [AvaloniaFact]
    public void EveryBodyColumnInANarrowWindowScrollsSidewaysWithItsHeader()
    {
        var memory = Memory();
        memory.Remember(BodySearchPage.TableId, EveryBodyColumn);
        var (panel, window) = Shown(Bodies(40), BodySearchPage.RootKey, memory, 900);

        var header = Grids(panel, BodySearchPage.TableId).Single(grid => grid.IsHeader);
        Assert.Equal(EveryBodyColumn, header.Layout.Columns.Select(column => column.Key));

        AssertScrollsTogether(window, BodySearchPage.TableId);

        var box = Box(header);
        var bar = RowScroller(panel).GetVisualDescendants().OfType<ScrollBar>().Single(scrollBar => scrollBar.Orientation == Avalonia.Layout.Orientation.Vertical);
        var barRight = bar.TranslatePoint(new Point(bar.Bounds.Width, 0), window)!.Value.X;
        var boxRight = box.TranslatePoint(new Point(box.Viewport.Width, 0), window)!.Value.X;
        Assert.True(bar.IsEffectivelyVisible && bar.Bounds.Width > 0, "the rows have no vertical bar");
        Assert.True(barRight <= boxRight + 0.5, $"scrolled to the end, the rows' bar ends at {barRight}, the box at {boxRight}");

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("bodies-every-column-scrolled.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheWheelOverTheRowsScrollsThemDownUnderAHeldHeader()
    {
        var memory = Memory();
        memory.Remember(BodySearchPage.TableId, EveryBodyColumn);
        var (panel, window) = Shown(Bodies(40), BodySearchPage.RootKey, memory, 900);

        var header = Grids(panel, BodySearchPage.TableId).Single(grid => grid.IsHeader);
        var box = Box(header);
        var rows = RowScroller(panel);
        Assert.True(rows.Extent.Height > rows.Viewport.Height, "the rows must scroll down for the wheel check");

        box.Offset = new Vector(100, 0);
        Settle();
        var headerY = header.TranslatePoint(default, window)!.Value.Y;

        var over = rows.TranslatePoint(new Point(box.Offset.X + 40, 40), window)!.Value;

        // Hit testing reads the last rendered scene, so render the scrolled one first.
        window.CaptureRenderedFrame()?.Dispose();
        Assert.Same(rows, window.GetVisualAt(over)!.FindAncestorOfType<ScrollViewer>(includeSelf: true));

        window.MouseWheel(over, new Vector(0, -1));
        Settle();

        Assert.True(rows.Offset.Y > 0, "the wheel over the rows did not scroll them down");
        Assert.Equal(headerY, header.TranslatePoint(default, window)!.Value.Y, 1);
        Assert.Equal(100, box.Offset.X, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void FiveHundredBodiesStillRealiseOnlyTheRowsInView()
    {
        var memory = Memory();
        memory.Remember(BodySearchPage.TableId, EveryBodyColumn);
        var (panel, window) = Shown(Bodies(500), BodySearchPage.RootKey, memory, 900);

        Assert.Equal(500, panel.GetVisualDescendants().OfType<ItemsControl>().Single(list => list.Name == GalaxySearchPage.RowsName).ItemCount);

        var realised = Grids(panel, BodySearchPage.TableId).Count(grid => !grid.IsHeader);
        Assert.InRange(realised, 1, 60);

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemsTableThatFitsDoesNotScrollSideways()
    {
        var systems = Enumerable.Range(1, 5).Select(n => new SystemSummary { Name = $"Alioth {n}", Distance = n }).ToList();
        var (panel, window) = Shown(Post(GalaxySearchKind.Systems, new GalaxySearchResult("LTT 7786", 5, systems)), SystemSearchPage.RootKey, Memory(), 1280);

        var header = Grids(panel, SystemSearchPage.TableId).Single(grid => grid.IsHeader);
        var box = Box(header);

        Assert.Equal(box.Viewport.Width, box.Extent.Width, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void RareGoodsWithSellsForInANarrowWindowScrollsSideways()
    {
        var memory = Memory();
        memory.Remember(RareGoodsPage.TableId, RareColumnsWithSellsFor);
        var rares = RareCatalogue.All.Take(12).Select((rare, n) => new RareListingRow(rare, null, 10 + n, 4_000)).ToList();
        var (panel, window) = Shown(Post(GalaxySearchKind.Rares, new RareSearchResult(rares, false)), RareGoodsPage.RootKey, memory, 900);

        Assert.Contains("SellsFor", Grids(panel, RareGoodsPage.TableId).Single(grid => grid.IsHeader).Layout.Columns.Select(column => column.Key));

        AssertScrollsTogether(window, RareGoodsPage.TableId);
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetSearchTableDoesNotScrollSideways()
    {
        var (panel, window) = Shown(Bodies(5), BodySearchPage.RootKey, Memory(headset: true), 900, headset: true);

        var header = Grids(panel, BodySearchPage.TableId).Single(grid => grid.IsHeader);

        Assert.Null(header.FindAncestorOfType<TableScroll>());
        window.Close();
    }
}
