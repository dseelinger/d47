using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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
        var window = new Window
        {
            Content = panel,
            Width = 960,
            Height = 930,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();
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

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        var plan = RouteMapStop.From(game.Plans.Last(RoutePlanKind.Riches)!)!;

        Assert.Equal(RoutePlanKind.Riches, page.PlanDrawn);
        Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.RootKeyOf(PanelTab.Navigation));
        Assert.Equal(2, page.Framing.SelectedIndex);
        Assert.Equal(GalaxyViewport.Fitting(Positioned(plan)), page.Map.Viewport);
        Assert.True(page.Map.Viewport.LightYearsAcross <= GalaxyMapView.DetailAcross);

        foreach (var stop in Enumerable.Range(0, 10))
        {
            Assert.NotNull(page.Map.ScreenOf($"Stop {stop}"));
        }

        Assert.Null(page.Map.ScreenOf("Hop 4"));
    }

    [AvaloniaFact]
    public void AnArrivalOnAPlanStopRedrawsTheReachedStop()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(-1, page.Map.Scene.Reached);
        Assert.False(page.Tick());

        game.Plans.Apply([Event(Arrival("FSDJump", Now, "Stop 5", At(6, 11)))]);

        Assert.True(page.Tick());
        Assert.Equal(RouteMapStop.ReachedIndex(game.Plans.Last(RoutePlanKind.Riches)!), page.Map.Scene.Reached);
        Assert.Equal(6, page.Map.Scene.Reached);
    }

    [AvaloniaFact]
    public void ANewPlotOfTheChosenKindRedrawsOnTheNextTick()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
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

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(page.PlanDrawn);
        Assert.Equal("Hip 22460", page.Map.Scene.Route![^1].System);
    }

    [AvaloniaFact]
    public void ShowingNoPlanDrawsThePlottedRouteAgain()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        Assert.Equal(RoutePlanKind.Riches, page.PlanDrawn);

        page.ShowPlan(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(page.PlanDrawn);
        Assert.Equal(2, page.Framing.SelectedIndex);
        Assert.Equal("Hip 22460", page.Map.Scene.Route![^1].System);
        Assert.Equal(GalaxyViewport.Fitting(Positioned(RouteMapStop.From(game.Route))), page.Map.Viewport);
    }

    [AvaloniaFact]
    public void OffDutyNoRouteIsDrawnWhateverPlanIsChosen()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);
        var page = Open(panel);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        game.OffDuty = "Someone Else";
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.Map.Drawn.Route);
        Assert.Null(page.PlanDrawn);
    }

    [AvaloniaFact]
    public void AChoiceMadeBeforeThePageIsBuiltDrawsThePlanOnceItIs()
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game, openMap: false);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);

        var page = Open(panel);

        Assert.Equal(RoutePlanKind.Riches, page.PlanDrawn);
        Assert.Equal(2, page.Framing.SelectedIndex);
    }

    [AvaloniaFact]
    public void APlanRecordedAfterTheChoiceIsDrawn()
    {
        var game = new Game();
        var panel = Panel(game);
        var page = Open(panel);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        Assert.Null(page.PlanDrawn);

        game.Plans.Record(RichesRoute(), "Road to Riches", Now);

        Assert.True(page.Tick());
        Assert.Equal(RoutePlanKind.Riches, page.PlanDrawn);
    }
}
