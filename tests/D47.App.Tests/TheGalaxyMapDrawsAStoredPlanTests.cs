using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Galaxy map draws the shown Commander's last stored plan in place of the plotted route (#1552).</summary>
public class TheGalaxyMapDrawsAStoredPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition Wregoe = new(-40.6, 37.2, 75.3);

    private sealed class Game
    {
        public CommanderGameState Commander { get; } = Located();

        public NavRoute Route { get; set; } = BubbleRoute();

        public TravelLog Travel { get; } = new(new MemoryFileSystem(), NullLogger.Instance);

        public string? OffDuty { get; set; }

        public RoutePlanBook Plans { get; } = new("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Arrival(string kind, DateTimeOffset at, string system, StarPosition position) =>
        "{\"timestamp\":\"" + at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\",\"event\":\"" + kind
        + "\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":99,\"StarPos\":["
        + position.X.ToString("0.###", CultureInfo.InvariantCulture) + "," + position.Y.ToString("0.###", CultureInfo.InvariantCulture)
        + "," + position.Z.ToString("0.###", CultureInfo.InvariantCulture) + "]}";

    private static CommanderGameState Located()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(Arrival("Location", Now, "Wregoe TC-X b29-0", Wregoe)));

        return state;
    }

    private static NavRoute BubbleRoute() => new()
    {
        Hops =
        [
            .. Enumerable.Range(0, 9).Select(index => new RouteHop(
                index == 0 ? "Wregoe TC-X b29-0" : index == 8 ? "Hip 22460" : $"Hop {index}",
                "G")
            {
                Position = (Wregoe.X - (index * 18), Wregoe.Y, Wregoe.Z + (index * 14)),
            }),
        ],
    };

    private static readonly StarPosition HopFour = new(Wregoe.X - (4 * 18), Wregoe.Y, Wregoe.Z + (4 * 14));

    private static readonly StarPosition Hip22460 = new(Wregoe.X - (8 * 18), Wregoe.Y, Wregoe.Z + (8 * 14));

    /// <summary>Which of Grey and A the route line is drawn in halfway from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static string LineInk(GalaxyMapPage page, StarPosition from, StarPosition to)
    {
        var map = page.Map();
        var (a, b) = (map.Projected(from), map.Projected(to));
        var window = (Window)TopLevel.GetTopLevel(page)!;
        var middle = map.TranslatePoint(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), window)!.Value;
        var grey = Ink(ThemeManager.GreyKey);
        var amber = Ink(ThemeManager.AKey);

        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();

        var bytes = new byte[4];
        var nearest = (Key: string.Empty, Apart: int.MaxValue);

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var offset = (((int)middle.Y + dy) * buffer.RowBytes) + (((int)middle.X + dx) * 4);
                Marshal.Copy(buffer.Address + offset, bytes, 0, 4);

                var drawn = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
                    ? Color.FromRgb(bytes[2], bytes[1], bytes[0])
                    : Color.FromRgb(bytes[0], bytes[1], bytes[2]);

                foreach (var (key, ink) in new[] { (ThemeManager.GreyKey, grey), (ThemeManager.AKey, amber) })
                {
                    if (Apart(drawn, ink) < nearest.Apart)
                    {
                        nearest = (key, Apart(drawn, ink));
                    }
                }
            }
        }

        return nearest.Key;
    }

    private static Color Ink(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static int Apart(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    private static StarPosition At(int index, int count) =>
        new(1000 * Math.Cos(index * 2 * Math.PI / count), 0, 1000 * Math.Sin(index * 2 * Math.PI / count));

    private static RichesRoute RichesRoute(bool positioned = true) =>
        new([.. Enumerable.Range(0, 10).Select(index => new RichesStop($"Stop {index}", 1, [])
        {
            Position = positioned ? At(index + 1, 11) : null,
        })])
        {
            Start = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
            End = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
        };

    private static PanelView Panel(Game game, bool openMap = true)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(
            () => game.Route,
            () => game.OffDuty is null ? game.Commander.Location.StarSystem : null,
            CapabilityRegistry.Build([]),
            game.Plans,
            Commodities: new CommodityBoard(),
            Settings: TestSurface.Settings(),
            Commander: () => game.Commander,
            Exobiology: () => null,
            Cargo: new BestCargoBoard(),
            OffDuty: what => game.OffDuty is null ? null : $"CMDR {game.OffDuty} is in the game; their {what} shows when they are the Commander shown.")
        {
            Travel = () => game.Travel,
            Now = () => Now,
        });

        panel.Tab = PanelTab.Navigation;

        if (openMap)
        {
            Assert.True(panel.Nav.SelectRoot(PanelTab.Navigation, RoutingPages.GalaxyMapRoot));
        }

        return panel;
    }

    private static GalaxyMapPage Open(PanelView panel)
    {
        Show(panel);

        return panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();
    }

    private static void Show(PanelView panel)
    {
        var window = new Window
        {
            Content = panel,
            Width = 960,
            Height = 930,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<StarPosition> Positioned(IEnumerable<RouteMapStop> stops) =>
        [.. stops.Where(stop => stop.Position is not null).Select(stop => stop.Position!.Value)];

    [AvaloniaFact]
    public void AStoredPlanReplacesThePlottedRouteAndIsFramed()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        var plan = RouteMapStop.From(game.Plans.Last(RoutePlanKind.Riches)!)!;

        Assert.Equal(RoutePlanKind.Riches, page.PlanShown());
        Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.RootKeyOf(PanelTab.Navigation));
        Assert.Equal(2, page.Framing().SelectedIndex);
        Assert.Equal(GalaxyViewport.Fitting(Positioned(plan))!.LightYearsAcross, page.Map().Viewport.LightYearsAcross);
        Assert.True(page.Map().Viewport.LightYearsAcross <= GalaxyMapView.DetailAcross);

        foreach (var stop in Enumerable.Range(0, 10))
        {
            Assert.Equal($"Stop {stop}", page.Map().SelectsAt(At(stop + 1, 11)));
        }

        Assert.NotEqual("Hop 4", page.Map().SelectsAt(HopFour));
    }

    [AvaloniaFact]
    public void AnArrivalOnAPlanStopRedrawsTheReachedStop()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeManager.AKey, LineInk(page, At(1, 11), At(2, 11)));
        Assert.False(page.Tick());

        game.Plans.Apply([Event(Arrival("FSDJump", Now, "Stop 5", At(6, 11)))]);

        Assert.True(page.Tick());
        Assert.Equal(6, RouteMapStop.ReachedIndex(game.Plans.Last(RoutePlanKind.Riches)!));
        Assert.Equal(ThemeManager.GreyKey, LineInk(page, At(1, 11), At(2, 11)));
        Assert.Equal(ThemeManager.GreyKey, LineInk(page, At(5, 11), At(6, 11)));
        Assert.Equal(ThemeManager.AKey, LineInk(page, At(6, 11), At(7, 11)));
    }

    [AvaloniaFact]
    public void ANewPlotOfTheChosenKindRedrawsOnTheNextTick()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        game.Plans.Record(RichesRoute(), "Road to Riches", Now);

        Assert.True(page.Tick());
        Assert.False(page.Tick());
    }

    [AvaloniaFact]
    public void APlanWithFewerThanTwoPositionedStopsDrawsThePlottedRoute()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(positioned: false), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(page.PlanShown());

        page.Framing().Option("Route").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Hip 22460", page.Map().SelectsAt(Hip22460));
        Assert.NotEqual("Stop 0", page.Map().SelectsAt(At(1, 11)));
    }

    [AvaloniaFact]
    public void ShowingNoPlanDrawsThePlottedRouteAgain()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        Assert.Equal(RoutePlanKind.Riches, page.PlanShown());

        page.ShowPlan(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(page.PlanShown());
        Assert.Equal(2, page.Framing().SelectedIndex);
        Assert.Equal("Hip 22460", page.Map().SelectsAt(Hip22460));
        Assert.Equal(GalaxyViewport.Fitting(Positioned(RouteMapStop.From(game.Route))), page.Map().Viewport);
    }

    [AvaloniaFact]
    public void OffDutyNoRouteIsDrawnWhateverPlanIsChosen()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        game.OffDuty = "Someone Else";
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.Map().Drawn.Route);
        Assert.Null(page.PlanShown());
    }

    [AvaloniaFact]
    public void AChoiceMadeBeforeThePageIsBuiltDrawsThePlanOnceItIs()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game, openMap: false);

        Show(panel);

        // Plan, the plan, and its Show on galaxy map press, which builds the map page for the first time.
        panel.Nav.SelectRoot(PanelTab.Navigation, RoutingPages.PlanRoot);
        panel.Nav.Drill(PanelTab.Navigation, RoutingPages.ResultCrumb(RoutePlanKind.Riches, "Road to Riches"));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(panel.GetVisualDescendants().OfType<GalaxyMapPage>());

        ThePlanPageOffersTheGalaxyMapTests.Press(panel.GetVisualDescendants().OfType<Button>().Single(button =>
            AutomationProperties.GetName(button) == RoutePlanResultPage.ShowOnGalaxyMapWords && button.Content is string));

        var page = panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

        Assert.Equal(RoutePlanKind.Riches, page.PlanShown());
        Assert.Equal(2, page.Framing().SelectedIndex);
    }

    [AvaloniaFact]
    public void APlanRecordedAfterTheChoiceIsDrawn()
    {
        var game = new Game();
        var panel = Panel(game);
        var page = Open(panel);

        page.ShowPlan(RoutePlanKind.Riches);
        Assert.Null(page.PlanShown());

        game.Plans.Record(RichesRoute(), "Road to Riches", Now);

        Assert.True(page.Tick());
        Assert.Equal(RoutePlanKind.Riches, page.PlanShown());
    }
}
