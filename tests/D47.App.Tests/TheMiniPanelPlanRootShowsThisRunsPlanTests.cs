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

/// <summary>The mini panel's Plan root draws the plan in play this run, and the Galaxy mini otherwise (#1926).</summary>
public class TheMiniPanelPlanRootShowsThisRunsPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Earlier = Now.AddDays(-3);

    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private sealed class Game
    {
        public CommanderGameState Commander { get; } = new(new CommanderIdentity("F1", "Kestrel Vane"));

        public NavRoute Route { get; set; } = NavRoute.None;

        public RoutePlanBook Plans { get; } = new("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
    }

    private static PlottedRoute Neutron() => new(
        "Sol",
        "Colonia",
        1_000,
        2,
        [new RouteWaypoint("Neutron Alpha", 1, 700, IsNeutron: true), new RouteWaypoint("Colonia", 1, 0, IsNeutron: false)]);

    private static RichesRoute Riches() =>
        new([.. Enumerable.Range(0, 3).Select(index => new RichesStop($"Stop {index}", index + 2, [])
        {
            Position = new StarPosition(index * 200, 0, 0),
        })]);

    private static NavRoute Plotted() => new()
    {
        Hops = [new RouteHop("Sol", "G") { Position = (0, 0, 0) }, new RouteHop("Hop One", "G") { Position = (40, 0, 20) }],
    };

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
            () => null,
            CapabilityRegistry.Build([]),
            game.Plans,
            Commodities: new CommodityBoard(),
            Settings: TestSurface.Settings(),
            Commander: () => game.Commander,
            Exobiology: () => null,
            Cargo: new BestCargoBoard())
        {
            Travel = () => new TravelLog(new MemoryFileSystem(), NullLogger.Instance),
            Now = () => Now,
        });

        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(PanelTab.Navigation, RoutingPages.PlanRoot);
        Assert.Equal(RoutingPages.PlanRoot, panel.Nav.RootKeyOf(PanelTab.Navigation));

        var window = new Window
        {
            Content = panel,
            Width = 792,
            Height = 360,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static RouteMini? Card(PanelView panel) =>
        panel.GetVisualDescendants().OfType<RouteMini>().SingleOrDefault(card => card.IsEffectivelyVisible);

    private static GalaxyMapMini? Map(PanelView panel) =>
        panel.GetVisualDescendants().OfType<GalaxyMapMini>().SingleOrDefault(map => map.IsEffectivelyVisible);

    private static string[] Texts(Control card) =>
        [.. card.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty)];

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static void Open(PanelView panel, RoutePlanKind kind, string headline)
    {
        panel.Nav.Drill(PanelTab.Navigation, RoutingPages.ResultCrumb(kind, headline));
        Settle();
    }

    [AvaloniaFact]
    public void AfterARestartStoredPlansGiveWayToTheGalaxyMini()
    {
        var game = new Game { Route = Plotted() };
        game.Plans.Record(Neutron(), "Sol to Colonia", Earlier);
        game.Plans.Record(Riches(), "Road to Riches", Earlier);

        var panel = Panel(game);

        Assert.Null(Card(panel));
        Assert.NotNull(Map(panel));
    }

    [AvaloniaFact]
    public void PlottingANeutronRoutePutsItOnTheCardUnderItsPlanTitle()
    {
        var game = new Game();
        var panel = Panel(game);

        game.Plans.Record(Neutron(), "Sol to Colonia", Later);
        Settle();

        var card = Card(panel)!;
        var texts = Texts(card);

        Assert.NotNull(card);
        Assert.Contains("JUMP PLAN:", texts);
        Assert.Contains("Sol to Colonia", texts);
        Assert.Contains("Neutron Alpha", texts);
        Assert.Contains("NEUTRON", texts);
        Assert.Null(Map(panel));
    }

    [AvaloniaFact]
    public void ShowMostRecentPutsTheRichesPlanOnTheCardInPlaceOfTheNeutronPlan()
    {
        var game = new Game();
        game.Plans.Record(Riches(), "Road to Riches", Earlier);

        var panel = Panel(game);

        game.Plans.Record(Neutron(), "Sol to Colonia", Later);
        Settle();
        Assert.Contains("JUMP PLAN:", Texts(Card(panel)!));

        Open(panel, RoutePlanKind.Riches, "Road to Riches");

        var texts = Texts(Card(panel)!);

        Assert.Contains("ROAD TO RICHES PLAN:", texts);
        Assert.Contains("Stop 0", texts);
        Assert.Contains("Stop 1", texts);
        Assert.Contains("Stop 2", texts);
        Assert.Contains("NEXT", texts);
        Assert.Contains("2 jumps", texts);
        Assert.Contains("4 jumps", texts);
        Assert.DoesNotContain("Neutron Alpha", texts);
    }

    [AvaloniaFact]
    public void ARouteAnotherCallerPlotsPutsItOnTheCardTheSameWay()
    {
        var game = new Game();
        var panel = Panel(game);

        game.Plans.Record(Neutron(), "Sol to Colonia", Later);
        game.Plans.Record(Riches(), "Road to Riches", Later.AddMinutes(1));
        Settle();

        Assert.Contains("ROAD TO RICHES PLAN:", Texts(Card(panel)!));
    }

    [AvaloniaFact]
    public void AMoveAlongAPlanDoesNotBringAnOlderPlanBack()
    {
        var game = new Game();
        game.Plans.Record(Riches(), "Road to Riches", Earlier);

        var panel = Panel(game);

        Assert.Null(Card(panel));

        game.Plans.Apply([]);
        Settle();

        Assert.Null(Card(panel));
    }

    [AvaloniaFact]
    public void WithNoPlanInPlayEliteRouteDrawsOnTheMiniAndClearingItSaysSo()
    {
        var game = new Game { Route = Plotted() };
        var panel = Panel(game);
        var map = Map(panel)!;

        Assert.Equal(["Sol", "Hop One"], map.Map.Scene.Route!.Select(stop => stop.System));

        game.Route = NavRoute.None;

        Assert.True(map.Tick());
        Assert.Equal("No route is plotted.", map.Note);
    }
}
