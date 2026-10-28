using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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

/// <summary>A stored plan's page has a Show on galaxy map press and a pressable route map (#1553).</summary>
public class ThePlanPageOffersTheGalaxyMapTests
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition Wregoe = new(-40.6, 37.2, 75.3);

    internal sealed class Game
    {
        public CommanderGameState Commander { get; } = Located();

        public TravelLog Travel { get; } = new(new MemoryFileSystem(), NullLogger.Instance);

        public string? OffDuty { get; set; }

        public RoutePlanBook Plans { get; } = new("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
    }

    private static CommanderGameState Located()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        var line = "{\"timestamp\":\"" + Now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            + "\",\"event\":\"Location\",\"StarSystem\":\"Wregoe TC-X b29-0\",\"SystemAddress\":99,\"StarPos\":["
            + string.Join(",", new[] { Wregoe.X, Wregoe.Y, Wregoe.Z }.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))) + "]}";
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        state.Apply(parsed!);

        return state;
    }

    private static StarPosition At(int index, int count) =>
        new(1000 * Math.Cos(index * 2 * Math.PI / count), 0, 1000 * Math.Sin(index * 2 * Math.PI / count));

    internal static RichesRoute RichesRoute(bool positioned = true) =>
        new([.. Enumerable.Range(0, 10).Select(index => new RichesStop($"Stop {index}", 1, [])
        {
            Position = positioned ? At(index + 1, 11) : null,
        })])
        {
            Start = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
            End = positioned ? new RouteEndpoint("Home", At(0, 11)) : null,
        };

    private static StoredRoutePlan Plan(RoutePlanKind kind, bool positioned = true) => kind switch
    {
        RoutePlanKind.Jump => new()
        {
            Kind = kind,
            Headline = "Sol to Colonia",
            Jump = new PlottedRoute(
                "Sol",
                "Colonia",
                22_000,
                20,
                [.. Enumerable.Range(0, 20).Select(index => new RouteWaypoint($"Waypoint {index}", 1, 100, false)
                {
                    Position = positioned ? new StarPosition(index * 150, 0, 300 * Math.Sin(index / 9.0)) : null,
                })]),
        },
        RoutePlanKind.Riches => new()
        {
            Kind = kind,
            Headline = "Road to Riches",
            Riches = RichesRoute(positioned),
        },
        RoutePlanKind.Exobiology => new()
        {
            Kind = kind,
            Headline = "Exobiology",
            Exobiology = new ExobiologyRoute(
                [.. Enumerable.Range(0, 10).Select(index => new ExobiologyStop($"Stop {index}", 1, [])
                {
                    Position = positioned ? At(index + 1, 11) : null,
                })]),
        },
        RoutePlanKind.Carrier => new()
        {
            Kind = kind,
            Headline = "Sol to Colonia",
            Carrier = new CarrierRoute(
            [
                .. Enumerable.Range(0, 12).Select(index => new CarrierWaypoint(
                    $"Stop {index}", index == 0 ? 0 : 450, 0, 500, 40, index % 4 == 3, 100, 0, false, false, index == 11)
                {
                    Position = positioned ? new StarPosition(index * 450, 0, 200 * Math.Sin(index)) : null,
                }),
            ]),
        },
        _ => new() { Kind = kind, Headline = "A trade run" },
    };

    public static TheoryData<RoutePlanKind> Drawable => new()
    {
        RoutePlanKind.Jump,
        RoutePlanKind.Riches,
        RoutePlanKind.Exobiology,
        RoutePlanKind.Carrier,
    };

    private static void Themed()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });
    }

    private static RoutePlanResultPage Open(StoredRoutePlan plan, Action<RoutePlanKind>? showOnGalaxyMap, Func<string, string?>? offDuty = null)
    {
        Themed();

        var page = new RoutePlanResultPage(plan, offDuty: offDuty, showOnGalaxyMap: showOnGalaxyMap);
        var window = new Window { Content = page, Width = 1000, Height = 700, Background = (IBrush?)Application.Current!.Resources[ThemeManager.BgKey] };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return page;
    }

    private static Button Named(Control page, string name, bool tile) =>
        page.GetVisualDescendants().OfType<Button>()
            .Where(button => Avalonia.Automation.AutomationProperties.GetName(button) == name)
            .Single(button => (button.Content is string) == tile);

    private static Button Tile(Control page) => Named(page, RoutePlanResultPage.ShowOnGalaxyMapWords, tile: true);

    private static Button MapPress(Control page) => Named(page, RoutePlanResultPage.ShowOnGalaxyMapWords, tile: false);

    internal static void Press(Button button)
    {
        Assert.True(button.IsVisible);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drawable))]
    public void APositionedPlanShowsThePressAndThePressableMap(RoutePlanKind kind)
    {
        RoutePlanKind? pressed = null;
        var page = Open(Plan(kind), chosen => pressed = chosen);

        Assert.True(Tile(page).IsVisible);
        Assert.True(MapPress(page).IsVisible);
        Assert.True(MapPress(page).IsHitTestVisible);

        Press(Tile(page));
        Assert.Equal(kind, pressed);

        pressed = null;
        Press(MapPress(page));
        Assert.Equal(kind, pressed);
    }

    [AvaloniaFact]
    public void ATradePlanHasNoPressAndNoPressableMap()
    {
        var page = Open(Plan(RoutePlanKind.Trade), _ => { });

        Assert.False(Tile(page).IsVisible);
        Assert.False(MapPress(page).IsVisible);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Drawable))]
    public void APlanWithFewerThanTwoPositionedStopsHasNoPress(RoutePlanKind kind)
    {
        var page = Open(Plan(kind, positioned: false), _ => { });

        Assert.False(Tile(page).IsVisible);
        Assert.False(MapPress(page).IsVisible);
    }

    [AvaloniaFact]
    public void OffDutyThePlanPageHasNoPress()
    {
        var page = Open(Plan(RoutePlanKind.Riches), _ => { }, _ => "CMDR Someone Else is in the game.");

        Assert.False(Tile(page).IsVisible);
        Assert.False(MapPress(page).IsVisible);
    }

    [AvaloniaFact]
    public void WithNoCallbackTheMapIsDrawnButNotPressable()
    {
        var page = Open(Plan(RoutePlanKind.Riches), null);

        Assert.False(Tile(page).IsVisible);
        Assert.True(page.GetVisualDescendants().OfType<RouteMapView>().Single().IsVisible);
        Assert.False(MapPress(page).IsHitTestVisible);
    }

    internal static PanelView Panel(Game game, bool headset, bool resultOpen = true)
    {
        Themed();

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
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
        panel.Nav.SelectRoot(PanelTab.Navigation, RoutingPages.PlanRoot);

        if (resultOpen)
        {
            panel.Nav.Drill(PanelTab.Navigation, RoutingPages.ResultCrumb(RoutePlanKind.Riches, "Road to Riches"));
        }
        else
        {
            Assert.True(panel.Nav.SelectRoot(PanelTab.Navigation, RoutingPages.GalaxyMapRoot));
        }

        return panel;
    }

    internal static void Show(PanelView panel, bool headset)
    {
        if (headset)
        {
            new OffscreenSurface(panel, new PixelSize(1024, 640)).Render();
            return;
        }

        new Window
        {
            Content = panel,
            Width = 960,
            Height = 930,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        }.Show();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressingShowOnGalaxyMapOpensTheMapOnThePlan(bool headset)
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game, headset);
        Show(panel, headset);

        Press(Tile(panel));

        Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.RootKeyOf(PanelTab.Navigation));
        Assert.Equal(RoutePlanKind.Riches, panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single().PlanDrawn);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressingTheRouteMapOpensTheMapOnThePlan(bool headset)
    {
        var game = new Game();
        game.Plans.Record(RichesRoute(), "Road to Riches", Now.AddHours(-1));
        var panel = Panel(game, headset);
        Show(panel, headset);

        Press(MapPress(panel));

        Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.RootKeyOf(PanelTab.Navigation));
        Assert.Equal(RoutePlanKind.Riches, panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single().PlanDrawn);
    }
}
