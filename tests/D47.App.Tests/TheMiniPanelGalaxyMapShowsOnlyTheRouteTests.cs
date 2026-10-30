using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
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

/// <summary>The mini panel's Galaxy map root shows the route still ahead and nothing else (#1916).</summary>
public class TheMiniPanelGalaxyMapShowsOnlyTheRouteTests
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

    private static RichesRoute ThreeStops() =>
        new([.. Enumerable.Range(0, 3).Select(index => new RichesStop($"Stop {index}", 1, [])
        {
            Position = new StarPosition(index * 200, 0, 0),
        })]);

    private static PanelView Panel(Game game)
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
        Assert.True(panel.Nav.SelectRoot(PanelTab.Navigation, RoutingPages.GalaxyMapRoot));

        var window = new Window
        {
            Content = panel,
            Width = 792,
            Height = 280,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static GalaxyMapMini Mini(PanelView panel) => panel.GetVisualDescendants().OfType<GalaxyMapMini>().Single();

    private static string[] Names(GalaxyMapMini mini) => [.. mini.Map.Scene.Route!.Select(stop => stop.System)];

    private static void Arrive(Game game, string system, StarPosition at) =>
        game.Commander.Apply(Event(Arrival("FSDJump", Now, system, at)));

    [AvaloniaFact]
    public void TheMiniPanelHoldsTheMapAloneWithNoControlsOfThePage()
    {
        var panel = Panel(new Game());
        var mini = Mini(panel);

        Assert.True(mini.IsEffectivelyVisible);
        Assert.Empty(mini.GetVisualDescendants().OfType<Segment>());
        Assert.Empty(mini.GetVisualDescendants().OfType<CheckBox>());
        Assert.Empty(mini.GetVisualDescendants().OfType<Button>());
        Assert.False(panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single().IsEffectivelyVisible);
        Assert.True(mini.Bounds.Width > 400 && mini.Bounds.Height > 200, mini.Bounds.ToString());
    }

    [AvaloniaFact]
    public void OnlyTheRouteAheadIsDrawnFromThePosition()
    {
        var mini = Mini(Panel(new Game()));
        var scene = mini.Map.Scene;

        Assert.Equal(["Wregoe TC-X b29-0", "Hop 1", "Hop 2", "Hop 3", "Hop 4", "Hop 5", "Hop 6", "Hop 7", "Hip 22460"], Names(mini));
        Assert.Null(scene.Travels);
        Assert.False(scene.Regions);
        Assert.Null(scene.Marks);
        Assert.Null(scene.Visited);
        Assert.Null(scene.Unvisited);
        Assert.Equal(0, scene.Reached);
        Assert.Equal(Wregoe, scene.Commander);
    }

    [AvaloniaFact]
    public void TheFrameHoldsThePositionAndEveryStopAheadWithTheInset()
    {
        var mini = Mini(Panel(new Game()));
        var width = mini.Map.Bounds.Width;
        var height = mini.Map.Bounds.Height;

        foreach (var name in Names(mini))
        {
            var at = mini.Map.ScreenOf(name)!.Value;

            Assert.InRange(at.X, 20 - 0.5, width - 20 + 0.5);
            Assert.InRange(at.Y, 20 - 0.5, height - 20 + 0.5);
        }

        var ys = Names(mini).Select(name => mini.Map.ScreenOf(name)!.Value.Y).ToList();

        Assert.Equal(20, ys.Min(), 0.5);
        Assert.Equal(height - 20, ys.Max(), 0.5);
    }

    [AvaloniaFact]
    public void AJumpAlongTheRouteRefitsToThePositionAndTheStopsLeft()
    {
        var game = new Game();
        var mini = Mini(Panel(game));
        var before = mini.Map.Viewport.LightYearsAcross;

        Arrive(game, "Hop 5", new StarPosition(Wregoe.X - 90, Wregoe.Y, Wregoe.Z + 70));

        Assert.True(mini.Tick());
        Assert.Equal(["Hop 5", "Hop 6", "Hop 7", "Hip 22460"], Names(mini));
        Assert.True(mini.Map.Viewport.LightYearsAcross < before);
        Assert.False(mini.Tick());
    }

    [AvaloniaFact]
    public void OffTheRouteTheFrameHoldsThePositionAndEveryStop()
    {
        var game = new Game();
        var mini = Mini(Panel(game));
        var away = new StarPosition(Wregoe.X + 300, Wregoe.Y, Wregoe.Z - 50);

        Arrive(game, "Elsewhere", away);

        Assert.True(mini.Tick());
        Assert.Equal(10, Names(mini).Length);
        Assert.Equal("Elsewhere", Names(mini)[0]);
        Assert.Equal(away.X, mini.Map.Scene.Commander!.Value.X, 3);
        Assert.Equal(away.Z, mini.Map.Scene.Commander!.Value.Z, 3);
        Assert.Equal(20, mini.Map.ScreenOf("Hip 22460")!.Value.X, 0.5);
    }

    [AvaloniaFact]
    public void WithThePositionUnknownTheFrameHoldsTheStopsAhead()
    {
        var unplaced = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        var route = BubbleRoute();

        Assert.Null(unplaced.Location.StarPos);

        var mini = new GalaxyMapMini(() => unplaced, () => route, () => null, null, () => null);
        var window = new Window { Content = mini, Width = 792, Height = 280 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(9, Names(mini).Length);
        Assert.Equal("Wregoe TC-X b29-0", Names(mini)[0]);
        Assert.Null(mini.Map.Scene.Commander);
        Assert.Equal(-1, mini.Map.Scene.Reached);
    }

    [AvaloniaFact]
    public void WithNoRouteTheRegionSaysSo()
    {
        var mini = Mini(Panel(new Game { Route = NavRoute.None }));

        Assert.Equal("No route is plotted.", mini.Note);
    }

    [AvaloniaFact]
    public void AStoredPlanAtItsLastStopSaysTheDestinationIsReached()
    {
        var game = new Game { Route = NavRoute.None };
        game.Plans.Record(ThreeStops(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game);

        panel.ShowOnGalaxyMap(RoutePlanKind.Riches);
        Dispatcher.UIThread.RunJobs();

        var mini = Mini(panel);

        Assert.True(mini.Tick());
        Assert.Equal(["Wregoe TC-X b29-0", "Stop 0", "Stop 1", "Stop 2"], Names(mini));
        Assert.Equal(0, mini.Map.Scene.Reached);

        game.Plans.Apply([Event(Arrival("FSDJump", Now, "Stop 2", new StarPosition(400, 0, 0)))]);

        Assert.True(mini.Tick());
        Assert.Equal("The destination is reached.", mini.Note);
    }

    [AvaloniaFact]
    public void AnotherCommandersGameShowsTheOffDutyLine()
    {
        var mini = Mini(Panel(new Game { OffDuty = "Wren" }));

        Assert.Equal($"CMDR Wren is in the game; their {OffDutyNote.Route} shows when they are the Commander shown.", mini.Note);
    }

    [AvaloniaFact]
    public void TheFullPanelStillShowsTheGalaxyMapPage()
    {
        var panel = Panel(new Game());
        var page = panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

        panel.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        Assert.True(page.IsEffectivelyVisible);
        Assert.False(Mini(panel).IsEffectivelyVisible);
        Assert.NotEmpty(page.GetVisualDescendants().OfType<Segment>());
        Assert.NotEmpty(page.GetVisualDescendants().OfType<CheckBox>());
    }
}
