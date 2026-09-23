using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every Routing page, rendered on each theme and at the three panel sizes and saved to
/// <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#403).
/// </summary>
public class TheRoutingScreensAreDrawnOnTheKitTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static readonly DateTimeOffset Plotted = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private const string Here = "Alpha Centauri";

    private sealed record Surface(Window Window, PanelView Panel);

    private static NavRoute Route() => new()
    {
        Hops =
        [
            new RouteHop("Sol", "G") { Position = (0, 0, 0) },
            new RouteHop(Here, "K") { Position = (3, 2, 1) },
            new RouteHop("Jackson's Lighthouse", "N") { Position = (12, 9, 5) },
            new RouteHop("Col 285 Sector AB-C d1", "Y") { Position = (18, 9, 5) },
            new RouteHop("Shinrarta Dezhra", "K") { Position = (22, 9, 5) },
        ],
    };

    private static RoutePlanBook Plans(string folder)
    {
        var plans = new RoutePlanBook(Path.Combine(folder, "route-plans.json"), NullLogger<RoutePlanBook>.Instance);

        plans.Record(
            new PlottedRoute(
                "Sol",
                "Colonia",
                22_000,
                168,
                [
                    new RouteWaypoint(Here, 1, 21_990, IsNeutron: false) { DistanceJumped = 4 },
                    new RouteWaypoint("Jackson's Lighthouse", 2, 21_500, IsNeutron: true) { DistanceJumped = 490 },
                    new RouteWaypoint("Eol Prou RS-T d3-94", 60, 11_000, IsNeutron: true) { DistanceJumped = 10_500 },
                    new RouteWaypoint("Colonia", 105, 0, IsNeutron: false) { DistanceJumped = 11_000 },
                ]),
            "Sol to Colonia",
            Plotted);

        plans.Record(
            new TradeRoute(
            [
                new TradeStop("Sol", "Abraham Lincoln")
                {
                    Buy = [new TradeLot("Gold", 700, 9_400)],
                    PricesSeen = Plotted,
                    PricesAreYours = true,
                },
                new TradeStop(Here, "Hutton Orbital")
                {
                    Distance = 4.4,
                    Jumps = 1,
                    DistanceToArrival = 6_784_000,
                    Sell = [new TradeLot("Gold", 700, 12_100)],
                    PricesSeen = Plotted,
                },
            ])
            {
                Capital = 10_000_000,
                TotalProfit = 1_890_000,
            },
            "Sol to Alpha Centauri",
            Plotted,
            currentSystem: "Sol");

        return plans;
    }

    private static CommodityBoard Board()
    {
        static MarketSnapshot Market(string station, string system, double x, int buy, int supply) => new()
        {
            Station = station,
            System = system,
            X = x,
            Type = "Orbis Starport",
            HasLargePad = true,
            DistanceToArrival = 300,
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-6),
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Tritium"] = new("Tritium") { BuyPrice = buy, Supply = supply },
            },
        };

        var board = new CommodityBoard();
        var query = new CommodityQuery("Tritium", TradeSide.Buying, Tonnes: 700);

        board.Post(new CommodityPosting(
            query,
            new CommodityAnswer(
                CommodityMarketSearch.Rank(
                    query,
                    [Market("Abraham Lincoln", "Sol", 0, 51_000, 9_000), Market("Hutton Orbital", Here, 4, 48_000, 1_200)],
                    Market("Home", "Sol", 0, 0, 0)),
                2,
                DroppedAsStale: 0,
                OriginKnown: true),
            "Sol",
            DateTimeOffset.UtcNow));

        return board;
    }

    private static Surface Open(double width, double height)
    {
        var folder = TempFolders.Create("d47-routing-capture");
        var (settings, _, _) = TestSurface.Create();

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        var goal = new CommunityGoalSurface(
            new CommunityGoalSearch { Showing = () => true },
            new CommodityLedger(),
            () => "F1234",
            () => DateTimeOffset.UtcNow,
            at => CommodityLedger.Week(at, DayOfWeek.Thursday, 7));

        panel.EnableRouting(new RoutingSurface(
            Route,
            () => Here,
            CapabilityRegistry.Build([]),
            Plans(folder),
            () => true,
            () => { },
            Board(),
            JumpRange: () => 64.2,
            CommunityGoal: goal,
            Clipboard: new D47.Core.Capabilities.Builtin.RecordingClipboard(),
            Settings: settings));

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel);
    }

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        return path;
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void EveryRoutingPageIsCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var surface = Open(width, height);
        var nav = surface.Panel.Nav;
        var saved = new List<string>();

        foreach (var root in new[]
                 {
                     RoutingPages.ProgressRoot,
                     RoutingPages.PlanRoot,
                     RoutingPages.CourseRoot,
                     RoutingPages.MarketRoot,
                     RoutingPages.CommunityGoalRoot,
                     RoutingPages.TradeRoot,
                 })
        {
            nav.SelectRoot(root);
            saved.Add(Save(surface.Window, $"routing-{root["routing.".Length..]}-{themeId}-{width}x{height}.png"));
        }

        nav.SelectRoot(RoutingPages.PlanRoot);
        nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
        saved.Add(Save(surface.Window, $"routing-result-jump-{themeId}-{width}x{height}.png"));

        nav.SelectRoot(RoutingPages.TradeRoot);
        nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Trade, "Sol to Alpha Centauri"));
        saved.Add(Save(surface.Window, $"routing-result-trade-{themeId}-{width}x{height}.png"));

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.All(saved, path => Assert.True(File.Exists(path)));
    }

    /// <summary>The Commander's current system is Cyan on Progress, decided from where the app says they are.</summary>
    [AvaloniaFact]
    public void TheCurrentSystemIsCyanOnProgress()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);
        surface.Panel.Nav.SelectRoot(RoutingPages.ProgressRoot);
        Dispatcher.UIThread.RunJobs();

        var names = surface.Panel.GetVisualDescendants().OfType<TextBlock>().ToList();

        Assert.Equal(Ink(ThemeManager.CyanKey), Foreground(names.First(block => block.Text == Here)));
        Assert.Equal(Ink(ThemeManager.AKey), Foreground(names.First(block => block.Text == "Shinrarta Dezhra")));

        surface.Window.Close();
    }

    /// <summary>A hop, waypoint or stop is a list row, and none of them carries a border.</summary>
    [AvaloniaFact]
    public void HopsAndWaypointsAreListRowsWithNoBorder()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);
        var nav = surface.Panel.Nav;

        nav.SelectRoot(RoutingPages.ProgressRoot);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(5, Rows(surface.Panel).Count);

        nav.SelectRoot(RoutingPages.PlanRoot);
        nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
        Dispatcher.UIThread.RunJobs();

        var rows = Rows(surface.Panel);

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal(default, row.BorderThickness));

        surface.Window.Close();
    }

    private static List<Border> Rows(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains(ListRow.Class))];

    private static Color? Foreground(TextBlock block) => (block.Foreground as ISolidColorBrush)?.Color;

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Theory]
    [InlineData("RoutingPages.cs")]
    [InlineData("RouteCoursePage.cs")]
    [InlineData("RoutePlanPage.cs")]
    [InlineData("RoutePlanResultPage.cs")]
    [InlineData("RouteProgressPage.cs")]
    [InlineData("RouteTradePage.cs")]
    [InlineData("RouteMarketPage.cs")]
    [InlineData("RouteCommunityGoalPage.cs")]
    [InlineData("RouteMini.cs")]
    public void TheRoutingSourceDrawsOnlyInTheNewTokens(string file)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "D47.App", "Panel", file));

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}\b", source);
        Assert.DoesNotMatch(
            @"ThemeManager\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)Key\b",
            source);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException($"No d47.slnx above {AppContext.BaseDirectory}.");
    }
}
