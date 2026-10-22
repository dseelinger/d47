using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>The Navigation tab's Galaxy map draws the Commander's travels, position and route, in the window and on the big panel (#1406).</summary>
public class TheGalaxyMapDrawsWhereTheCommanderHasBeenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly StarPosition Wregoe = new(-40.6, 37.2, 75.3);

    private sealed class Game
    {
        public CommanderGameState Commander { get; set; } = Located("Wregoe TC-X b29-0", Wregoe);

        public NavRoute Route { get; set; } = BubbleRoute();

        public TravelLog Travel { get; } = new(new MemoryFileSystem(), NullLogger.Instance);

        public BookmarkStore Bookmarks { get; } = new("bookmarks.json", new MemoryFileSystem(), NullLogger<BookmarkStore>.Instance);

        public string? OffDuty { get; set; }
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Arrival(string kind, DateTimeOffset at, long address, string system, StarPosition position) =>
        "{\"timestamp\":\"" + at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\",\"event\":\"" + kind
        + "\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address
        + ",\"StarPos\":[" + Coordinate(position.X) + "," + Coordinate(position.Y) + "," + Coordinate(position.Z) + "]}";

    private static string Coordinate(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static CommanderGameState Located(string system, StarPosition at)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Kestrel Vane"));
        state.Apply(Event(Arrival("Location", Now, 99, system, at)));

        return state;
    }

    private static NavRoute BubbleRoute() => new()
    {
        Hops =
        [
            .. Enumerable.Range(0, 9).Select(index => new RouteHop(
                index == 0 ? "Wregoe TC-X b29-0" : index == 8 ? "Hip 22460" : $"Hop {index}",
                index == 3 ? "N" : index == 5 ? "DA" : index == 6 ? "M" : "G")
            {
                Position = (Wregoe.X - (index * 18), Wregoe.Y, Wregoe.Z + (index * 14) + (8 * Math.Sin(index))),
            }),
        ],
    };

    /// <summary>A career: a bubble wander a year and a half ago, a trip to Colonia this year and a short hop this month.</summary>
    private static IEnumerable<string> Career()
    {
        yield return "{\"timestamp\":\"2025-01-01T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Kestrel Vane\"}";

        var at = Now.AddDays(-600);
        var address = 1000L;

        yield return Arrival("Location", at, address++, "Sol", new StarPosition(0, 0, 0));

        for (var step = 1; step <= 60; step++)
        {
            at = at.AddHours(3);
            yield return Arrival("FSDJump", at, address++, $"Bubble {step}", new StarPosition(80 * Math.Sin(step / 3.0), 0, 60 * Math.Cos(step / 4.0)));
        }

        at = Now.AddDays(-200);
        yield return Arrival("Location", at, address++, "Shinrarta Dezhra", new StarPosition(55.7, 17.6, 27.2));

        for (var step = 1; step <= 40; step++)
        {
            at = at.AddHours(1);
            yield return Arrival(step % 10 == 0 ? "CarrierJump" : "FSDJump", at, address++, $"Out {step}", new StarPosition(-9530.5 * step / 40, 0, 19808 * step / 40));
        }

        at = Now.AddDays(-10);
        yield return Arrival("Location", at, address++, "Colonia", new StarPosition(-9530.5, -910.3, 19808.1));

        for (var step = 1; step <= 20; step++)
        {
            at = at.AddHours(2);
            var back = Wregoe with { X = -9530.5 + ((Wregoe.X + 9530.5) * step / 20), Z = 19808.1 + ((Wregoe.Z - 19808.1) * step / 20) };
            yield return Arrival("FSDJump", at, address++, step == 20 ? "Wregoe TC-X b29-0" : $"Home {step}", back);
        }
    }

    private static Game Played(bool folded = true, bool history = true)
    {
        var game = new Game();

        if (history)
        {
            game.Travel.Fold(Career());
        }

        if (folded)
        {
            game.Travel.FoldHistory([]);
        }

        return game;
    }

    private static void Themed()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });
    }

    /// <summary>The Navigation tab with every one of its pages, so its sub-tab row is as long as the app's.</summary>
    private static PanelView Panel(Game game, bool headset)
    {
        Themed();

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        var folder = TestSurface.MemoryFolder("d47-galaxy-map-tests");
        var files = new MemoryFileSystem();

        panel.EnableRouting(new RoutingSurface(
            () => game.Route,
            () => game.OffDuty is null ? game.Commander.Location.StarSystem : null,
            CapabilityRegistry.Build([]),
            new RoutePlanBook(Path.Combine(folder, "route-plans.json"), files, NullLogger<RoutePlanBook>.Instance),
            Commodities: new CommodityBoard(),
            Settings: TestSurface.Settings(),
            Bookmarks: game.Bookmarks,
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

        return panel;
    }

    private static (Window Window, PanelView Panel, GalaxyMapPage Page) InWindow(Game game)
    {
        var panel = Panel(game, headset: false);
        var window = new Window
        {
            Content = panel,
            Width = 960,
            Height = 930,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel, Page(panel));
    }

    private static (OffscreenSurface Surface, PanelView Panel, GalaxyMapPage Page) OnThePanel(Game game)
    {
        var panel = Panel(game, headset: true);
        var surface = new OffscreenSurface(panel, new PixelSize(1024, 640));
        surface.Render();

        return (surface, panel, Page(panel));
    }

    private static GalaxyMapPage Page(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static Button Tile(Control page, string says) =>
        page.GetVisualDescendants().OfType<Button>().Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == says);

    private static CheckBox Box(Control page, string label) =>
        page.GetVisualDescendants().OfType<CheckBox>().Single(box => (box.Content as string) == label);

    private static RadioButton Option(Segment segment, string label) =>
        segment.GetVisualDescendants().OfType<RadioButton>().Single(radio => (radio.Content as string) == label);

    private static bool Tap(OffscreenSurface surface, PanelView panel, Control control)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), panel)!.Value;
        var pressed = surface.Click(centre);
        surface.Render();

        return pressed;
    }

    [AvaloniaFact]
    public void GalaxyMapIsTheSecondPageOfTheNavigationTab()
    {
        var panel = Panel(Played(), headset: false);

        var roots = panel.Nav.Roots(PanelTab.Navigation).Select(root => root.Key).ToList();

        Assert.Equal(8, roots.Count);
        Assert.Equal(RoutingPages.GalaxyMapRoot, roots[1]);
        Assert.Equal("Galaxy map", panel.Nav.Roots(PanelTab.Navigation)[1].Word);
    }

    [AvaloniaFact]
    public void TheWindowOpensHereAndDrawsTheWholeGalaxyOnGalaxy()
    {
        var (window, _, page) = InWindow(Played());

        Assert.Equal(1, page.Framing.SelectedIndex);
        Assert.Equal(GalaxyViewport.Around(Wregoe), page.Map.Viewport);
        Assert.True(page.Map.Drawn.Travels);
        Assert.True(page.Map.Drawn.Route);
        Assert.True(page.Map.Drawn.You);
        Assert.True(page.Map.Drawn.Destination);
        Assert.Equal("120 jumps to 123 systems", page.CountLine);
        Assert.True(page.Map.Bounds.Height >= GalaxyMapPage.WindowMapHeight);

        var before = page.Map.Bounds.Height;
        window.Height = 1200;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(before + 270, page.Map.Bounds.Height, 1);
        window.Height = 930;
        Dispatcher.UIThread.RunJobs();

        using (var here = window.CaptureRenderedFrame()!)
        {
            here.SaveCapture("galaxy-map-window-here.png");
        }

        Option(page.Framing, "Galaxy").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(GalaxyViewport.WholeGalaxy, page.Map.Viewport);
        Assert.True(page.Map.Drawn.StopsHidden);

        using var galaxy = window.CaptureRenderedFrame()!;
        galaxy.SaveCapture("galaxy-map-window-galaxy.png");
    }

    [AvaloniaFact]
    public void TheBigPanelFitsThePageAndDrawsBothFramings()
    {
        var (surface, panel, page) = OnThePanel(Played());

        using (surface)
        {
            surface.Render();

            Assert.Equal(GalaxyMapPage.PanelMapHeight, page.Map.Bounds.Height);

            var top = page.TranslatePoint(default, panel)!.Value.Y;
            var lowest = page.GetVisualDescendants().OfType<Control>()
                .Where(control => control.GetSelfAndVisualAncestors().TakeWhile(visual => visual != page).All(visual => visual.IsVisible) && control.Bounds.Height > 0)
                .Max(control => control.TranslatePoint(new Point(0, control.Bounds.Height), panel)!.Value.Y);
            Assert.True(lowest <= top + page.Bounds.Height, $"The page starts at {top}, has {page.Bounds.Height} and draws down to {lowest}.");

            surface.Render().SaveCapture("galaxy-map-panel-here.png");

            Assert.True(Tap(surface, panel, Option(page.Framing, "Galaxy")));
            Assert.Equal(GalaxyViewport.WholeGalaxy, page.Map.Viewport);

            surface.Render().SaveCapture("galaxy-map-panel-galaxy.png");
        }
    }

    [AvaloniaFact]
    public void EveryControlWorksByOneTapOnTheBigPanel()
    {
        var (surface, panel, page) = OnThePanel(Played());

        using (surface)
        {
            var start = page.Map.Viewport;

            Assert.True(Tap(surface, panel, Tile(page, "Zoom in")));
            Assert.Equal(100, page.Map.Viewport.LightYearsAcross);
            Assert.Equal(-1, page.Framing.SelectedIndex);
            Assert.False(Tile(page, "Zoom in").IsEnabled);

            Assert.True(Tap(surface, panel, Tile(page, "Pan right")));
            Assert.Equal(start.CentreX + 25, page.Map.Viewport.CentreX, 6);

            Assert.True(Tap(surface, panel, Tile(page, "Pan up")));
            Assert.Equal(start.CentreZ + 25, page.Map.Viewport.CentreZ, 6);

            Assert.False(page.Map.Drawn.Destination);
            surface.Render().SaveCapture("galaxy-map-panel-zoomed.png");

            Assert.True(Tap(surface, panel, Tile(page, "Pan left")));
            Assert.True(Tap(surface, panel, Tile(page, "Pan down")));
            Assert.Equal(start.CentreX, page.Map.Viewport.CentreX, 6);
            Assert.Equal(start.CentreZ, page.Map.Viewport.CentreZ, 6);

            Assert.True(Tap(surface, panel, Tile(page, "Zoom out")));
            Assert.Equal(400, page.Map.Viewport.LightYearsAcross);

            Assert.True(Tap(surface, panel, Option(page.Framing, "Here")));
            Assert.Equal(GalaxyViewport.Around(Wregoe), page.Map.Viewport);

            Assert.True(Tap(surface, panel, Option(page.Framing, "Route")));
            Assert.Equal(2, page.Framing.SelectedIndex);
            Assert.NotEqual(GalaxyViewport.Around(Wregoe), page.Map.Viewport);

            Assert.True(Tap(surface, panel, Option(page.Range, "30 days")));
            Assert.Equal("20 jumps to 21 systems", page.CountLine);

            Assert.True(Tap(surface, panel, Option(page.Range, "1 year")));
            Assert.Equal("60 jumps to 62 systems", page.CountLine);

            Assert.True(Tap(surface, panel, Box(page, "Travels")));
            Assert.False(page.Map.Drawn.Travels);

            Assert.True(Tap(surface, panel, Box(page, "Route")));
            Assert.False(page.Map.Drawn.Route);

            Assert.True(Tap(surface, panel, Box(page, "Regions")));
            Assert.False(Box(page, "Regions").IsChecked);

            surface.Render().SaveCapture("galaxy-map-panel-switched-off.png");
        }
    }

    [AvaloniaFact]
    public void AGreyedControlTappedOnTheBigPanelDoesNothing()
    {
        var (surface, panel, page) = OnThePanel(Played());

        using (surface)
        {
            Assert.True(Tap(surface, panel, Option(page.Framing, "Galaxy")));
            Assert.False(Tile(page, "Zoom out").IsEnabled);

            Tap(surface, panel, Tile(page, "Zoom out"));
            Assert.Equal(GalaxyViewport.WholeGalaxy, page.Map.Viewport);

            var game = Played();
            game.Route = NavRoute.None;
            var (bare, barePanel, barePage) = OnThePanel(game);

            using (bare)
            {
                Assert.False(Option(barePage.Framing, "Route").IsEnabled);

                var before = barePage.Map.Viewport;
                Tap(bare, barePanel, Option(barePage.Framing, "Route"));

                Assert.Equal(before, barePage.Map.Viewport);
                Assert.Equal(1, barePage.Framing.SelectedIndex);
            }
        }
    }

    [AvaloniaFact]
    public void AJumpAndANewRouteRedrawWithoutMovingTheView()
    {
        var game = Played();
        var (_, _, page) = InWindow(game);

        page.Map.View(page.Map.Viewport.ZoomOut().Pan(1, 0));
        var view = page.Map.Viewport;
        var before = page.CountLine;

        var next = new StarPosition(Wregoe.X + 30, Wregoe.Y, Wregoe.Z + 10);
        var jump = Arrival("FSDJump", Now.AddMinutes(5), 5000, "Next Door", next);
        game.Travel.Apply([Event("{\"timestamp\":\"2026-10-21T12:01:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Kestrel Vane\"}"), Event(jump)]);
        game.Commander = Located("Next Door", next);

        Assert.True(page.Tick());
        Assert.Equal(view, page.Map.Viewport);
        Assert.NotEqual(before, page.CountLine);
        Assert.False(page.Tick());

        game.Route = BubbleRoute() with { Hops = [.. BubbleRoute().Hops.Take(4)] };

        Assert.True(page.Tick());
        Assert.Equal(view, page.Map.Viewport);
    }

    [AvaloniaFact]
    public void RegionNamesAreDrawnAtSixThousandFourHundredLightYearsAndWider()
    {
        var (_, _, page) = InWindow(Played());

        page.Map.View(GalaxyViewport.Around(Wregoe) with { LightYearsAcross = 6_400 });
        Assert.True(page.Map.Drawn.RegionNames);
        Assert.True(page.Map.Drawn.Destination);
        Assert.False(page.Map.Drawn.StopsHidden);

        page.Map.View(page.Map.Viewport.ZoomIn());
        Assert.False(page.Map.Drawn.RegionNames);

        page.Map.View(GalaxyViewport.WholeGalaxy);
        Assert.True(page.Map.Drawn.RegionNames);
        Assert.True(page.Map.Drawn.StopsHidden);
    }

    [AvaloniaFact]
    public void FilesFoldedByTheBackfillDoNotRedrawTheMapUntilItFinishes()
    {
        var game = Played(history: false, folded: false);
        var (_, _, page) = InWindow(game);

        game.Travel.Fold(Career().Take(30));
        Assert.False(page.Tick());

        game.Travel.Fold(Career().Take(1).Concat(Career().Skip(30)));
        Assert.False(page.Tick());

        game.Travel.FoldHistory([]);
        Assert.True(page.Tick());
        Assert.Equal("120 jumps to 123 systems", page.CountLine);
    }

    [AvaloniaFact]
    public void ACommanderWithNoTravelsSeesTheRegionsAndTheirPositionOnly()
    {
        var game = Played(history: false);
        game.Route = NavRoute.None;
        var (window, _, page) = InWindow(game);

        Assert.Equal(GalaxyMapPage.NoTravels, page.CountLine);
        Assert.False(page.Map.Drawn.Travels);
        Assert.False(page.Map.Drawn.Route);
        Assert.True(page.Map.Drawn.You);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("galaxy-map-no-travels.png");
    }

    [AvaloniaFact]
    public void BeforeTheBackfillFinishesThePageSaysEarlierJournalsAreBeingRead()
    {
        var game = Played(folded: false);
        var (_, _, page) = InWindow(game);

        Assert.Equal(GalaxyMapPage.Reading, page.CountLine);

        game.Travel.FoldHistory([]);

        Assert.True(page.Tick());
        Assert.Equal("120 jumps to 123 systems", page.CountLine);
    }

    [AvaloniaFact]
    public void ARangeWithNoJumpsSaysSo()
    {
        var game = new Game();
        game.Travel.Fold(Career().Take(20));
        game.Travel.FoldHistory([]);
        var (_, _, page) = InWindow(game);

        Option(page.Range, "30 days").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("No jumps in the last 30 days.", page.CountLine);

        Option(page.Range, "1 year").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("No jumps in the last year.", page.CountLine);
    }

    [AvaloniaFact]
    public void TheClosestAndWidestZoomDisableTheirButton()
    {
        var (_, _, page) = InWindow(Played());

        Tile(page, "Zoom in").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(100, page.Map.Viewport.LightYearsAcross);
        Assert.False(Tile(page, "Zoom in").IsEnabled);
        Assert.True(Tile(page, "Zoom out").IsEnabled);

        Option(page.Framing, "Galaxy").IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(Tile(page, "Zoom in").IsEnabled);
        Assert.False(Tile(page, "Zoom out").IsEnabled);
    }

    [Fact]
    public void RegionNamesWrapAtFourteenCharacters()
    {
        Assert.Equal(["OUTER ORION", "SPUR"], GalaxyMapView.Wrap("OUTER ORION SPUR", 14));
        Assert.Equal(["GALACTIC", "CENTRE"], GalaxyMapView.Wrap("GALACTIC CENTRE", 14));
        Assert.Equal(["ELYSIAN SHORE"], GalaxyMapView.Wrap("ELYSIAN SHORE", 14));
    }

    [Fact]
    public void TheScaleBarIsAQuarterOfTheView()
    {
        Assert.Equal("100 LY", GalaxyMapView.ScaleWords(400));
        Assert.Equal("25,600 LY", GalaxyMapView.ScaleWords(102_400));
    }
    private static string HopFour()
    {
        var down = 18 * 4;
        var up = (14 * 4) + (8 * Math.Sin(4));

        return Math.Sqrt((down * down) + (up * up)).ToString("N1", CultureInfo.CurrentCulture);
    }

    private static Point MapPoint(GalaxyMapPage page, string system) => page.Map.ScreenOf(system)!.Value;

    private static void TapMapInWindow(Window window, GalaxyMapPage page, Point at)
    {
        var inWindow = page.Map.TranslatePoint(at, window)!.Value;
        window.MouseDown(inWindow, Avalonia.Input.MouseButton.Left);
        window.MouseUp(inWindow, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool TapMapOnPanel(OffscreenSurface surface, PanelView panel, GalaxyMapPage page, Point at)
    {
        var pressed = surface.Click(page.Map.TranslatePoint(at, panel)!.Value);
        surface.Render();

        return pressed;
    }

    [AvaloniaFact]
    public void ATapOnAStarInTheWindowSelectsItAndATapOnNothingClearsIt()
    {
        var (window, _, page) = InWindow(Played());

        Assert.False(page.StripShown);

        TapMapInWindow(window, page, MapPoint(page, "Hop 4"));

        Assert.True(page.StripShown);
        Assert.Equal("HOP 4", page.StripName);
        Assert.Equal($"{HopFour()} LY away · {GalaxyMapPage.NotVisited}", page.StripFacts);
        Assert.Equal("Hop 4", page.Map.Selected!.Name);

        using (var selected = window.CaptureRenderedFrame()!)
        {
            selected.SaveCapture("galaxy-map-window-selected.png");
        }

        TapMapInWindow(window, page, MapPoint(page, "Hop 4") + new Point(0, 60));
        Assert.False(page.StripShown);
        Assert.Null(page.Map.Selected);

        TapMapInWindow(window, page, MapPoint(page, "Wregoe TC-X b29-0"));
        Assert.Equal("WREGOE TC-X B29-0", page.StripName);
        Assert.StartsWith($"{GalaxyMapPage.YouAreHere} · arrived once, last ", page.StripFacts);
    }

    [AvaloniaFact]
    public void ATapOnAStarOnTheBigPanelSelectsItAndItsButtonsWorkByOneTap()
    {
        var game = Played();
        var (surface, panel, page) = OnThePanel(game);

        using (surface)
        {
            Assert.True(TapMapOnPanel(surface, panel, page, MapPoint(page, "Hop 4")));
            Assert.True(page.StripShown);
            Assert.Equal($"{HopFour()} LY away · {GalaxyMapPage.NotVisited}", page.StripFacts);
            Assert.Equal(TypeScale.MinimumTarget, Tile(page, GalaxyMapPage.CentreHere).Bounds.Height);
            Assert.Equal(TypeScale.MinimumTarget, Tile(page, GalaxyMapPage.Bookmark).Bounds.Height);

            surface.Render().SaveCapture("galaxy-map-panel-selected.png");

            var star = page.Map.Selected!.At;
            var across = page.Map.Viewport.LightYearsAcross;

            Assert.True(Tap(surface, panel, Tile(page, GalaxyMapPage.CentreHere)));
            Assert.Equal(star.X, page.Map.Viewport.CentreX, 6);
            Assert.Equal(star.Z, page.Map.Viewport.CentreZ, 6);
            Assert.Equal(across, page.Map.Viewport.LightYearsAcross);
            Assert.True(page.StripShown);

            Assert.True(Tap(surface, panel, Tile(page, GalaxyMapPage.Bookmark)));
            Assert.Equal("Bookmarked Hop 4.", page.StripReply);

            Assert.True(Tap(surface, panel, Tile(page, GalaxyMapPage.Bookmark)));
            Assert.Equal("Hop 4 is already bookmarked under that name. The Bookmarks page renames it.", page.StripReply);

            surface.Render().SaveCapture("galaxy-map-panel-bookmarked.png");

            Assert.Equal(TypeScale.MinimumTarget, page.StripControl.Bounds.Height);

            var top = page.TranslatePoint(default, panel)!.Value.Y;
            var lowest = page.GetVisualDescendants().OfType<Control>()
                .Where(control => control.GetSelfAndVisualAncestors().TakeWhile(visual => visual != page).All(visual => visual.IsVisible) && control.Opacity > 0 && control.Bounds.Height > 0)
                .Max(control => control.TranslatePoint(new Point(0, control.Bounds.Height), panel)!.Value.Y);
            Assert.True(lowest <= top + page.Bounds.Height, $"The page starts at {top}, has {page.Bounds.Height} and draws down to {lowest}.");

            Assert.True(Tap(surface, panel, Tile(page, GalaxyMapPage.ClearSelection)));
            Assert.False(page.StripShown);
            Assert.Null(page.Map.Selected);

            Assert.True(TapMapOnPanel(surface, panel, page, MapPoint(page, "Hop 4") + new Point(20, -20)));
            Assert.Equal("Hop 4", page.Map.Selected!.Name);

            Assert.True(TapMapOnPanel(surface, panel, page, new Point(page.Map.Bounds.Width / 2, 8)));
            Assert.False(page.StripShown);
        }
    }

    [AvaloniaFact]
    public void ASelectionSurvivesARedrawAndALayerSwitchedOffAndLosesOnlyItsBrackets()
    {
        var (window, _, page) = InWindow(Played());

        TapMapInWindow(window, page, MapPoint(page, "Hop 4"));
        page.Refresh();

        Assert.True(page.StripShown);
        Assert.Equal("Hop 4", page.Map.Selected!.Name);

        Box(page, "Route").IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.True(page.StripShown);
        Assert.Equal("Hop 4", page.Map.Selected!.Name);
        Assert.Null(page.Map.ScreenOf("Hop 4"));

        Box(page, "Route").IsChecked = true;
        Option(page.Framing, "Galaxy").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(page.StripShown);

        Tile(page, GalaxyMapPage.ClearSelection).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(page.StripShown);
    }

    private const string Bubble5 = "Bubble 5";
    private const string Bubble9 = "Bubble 9";
    private const string Bubble12 = "Bubble 12";

    /// <summary>A bookmark, the carrier and two stored ships in visited systems, and one of each in a system never visited.</summary>
    private static Game Keeping()
    {
        var game = Played();

        game.Bookmarks.Add("F1", "Near the bar", Bubble5, Now);
        game.Bookmarks.Add("F1", "Lost cause", "Nowhere Known", Now);

        foreach (var line in new[]
        {
            """{"timestamp":"2026-10-21T11:00:00Z","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","FuelLevel":792}""",
            """{"timestamp":"2026-10-21T11:00:01Z","event":"CarrierLocation","CarrierType":"FleetCarrier","CarrierID":3715429376,"StarSystem":"Bubble 9","SystemAddress":1009,"BodyID":0}""",
            """{"timestamp":"2026-10-21T11:01:00Z","event":"StoredShips","StarSystem":"Wregoe TC-X b29-0","StationName":"Home Base","ShipsHere":[],"ShipsRemote":[{"ShipID":12,"ShipType":"cobramkv","ShipType_Localised":"Cobra MkV","Name":"Reaper","Value":1,"StarSystem":"Bubble 12"},{"ShipID":13,"ShipType":"python","ShipType_Localised":"Python","Value":1,"StarSystem":"Bubble 12"},{"ShipID":14,"ShipType":"anaconda","ShipType_Localised":"Anaconda","Name":"Big Slow","Value":1,"StarSystem":"Nowhere Known"}]}""",
        })
        {
            game.Commander.Apply(Event(line));
        }

        return game;
    }

    private static Engineer BubbleEngineer() => EngineerDirectory.All
        .Where(engineer => engineer.Position is not null && engineer.System is not null)
        .MinBy(engineer => engineer.Position!.Value.DistanceTo(StarPosition.Origin))!;

    private static void KeepCarrierIn(Game game, string system)
    {
        game.Commander.Apply(Event("""{"timestamp":"2026-10-21T11:00:00Z","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire"}"""));
        game.Commander.Apply(Event("{\"timestamp\":\"2026-10-21T11:00:01Z\",\"event\":\"CarrierLocation\",\"CarrierType\":\"FleetCarrier\",\"CarrierID\":3715429376,\"StarSystem\":\"" + system + "\",\"SystemAddress\":1009,\"BodyID\":0}"));
    }

    [AvaloniaFact]
    public void ABookmarkTheCarrierAndStoredShipsInVisitedSystemsAreDrawnAndSelectableInTheWindow()
    {
        var (window, _, page) = InWindow(Keeping());

        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble5));
        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Carrier, Bubble9));
        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Ships, Bubble12));

        TapMapInWindow(window, page, page.Map.MarkScreenOf(GalaxyMapMarkKind.Carrier, Bubble9)!.Value);

        Assert.Equal(Bubble9.ToUpperInvariant(), page.StripName);
        Assert.Equal(["Your carrier: BNH-T2F"], page.StripKinds);

        TapMapInWindow(window, page, page.Map.MarkScreenOf(GalaxyMapMarkKind.Ships, Bubble12)!.Value);

        Assert.Equal(["Stored ships: Reaper and Python"], page.StripKinds);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("galaxy-map-window-markers.png");
    }

    [AvaloniaFact]
    public void TheMarkersAreDrawnAndSelectableOnTheBigPanel()
    {
        var (surface, panel, page) = OnThePanel(Keeping());

        using (surface)
        {
            Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble5));
            Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Carrier, Bubble9));
            Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Ships, Bubble12));

            surface.Render().SaveCapture("galaxy-map-panel-markers.png");

            Assert.True(TapMapOnPanel(surface, panel, page, page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble5)!.Value));
            Assert.Equal(Bubble5.ToUpperInvariant(), page.StripName);
            Assert.Equal(["Bookmark: Near the bar"], page.StripKinds);

            surface.Render().SaveCapture("galaxy-map-panel-markers-selected.png");
        }
    }

    [AvaloniaFact]
    public void ItemsInSystemsNeverVisitedAreNotDrawnAndTheCountLineSaysSo()
    {
        var (_, _, page) = InWindow(Keeping());

        Assert.Equal("1 bookmark and 1 stored ship are in systems you have not visited.", page.UnplacedLine);

        page.BookmarkSwitch.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("1 stored ship is in a system you have not visited.", page.UnplacedLine);

        page.ShipSwitch.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(string.Empty, page.UnplacedLine);
    }

    [AvaloniaFact]
    public void TheCountLineNamesTheCarrierAndPluralsEachKind()
    {
        var game = Played();
        game.Bookmarks.Add("F1", "A", "Nowhere Known", Now);
        game.Bookmarks.Add("F1", "B", "Nowhere Else", Now);
        KeepCarrierIn(game, "Nowhere Known");
        game.Commander.Apply(Event("""{"timestamp":"2026-10-21T11:01:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"X","ShipsHere":[],"ShipsRemote":[{"ShipID":14,"ShipType":"anaconda","Name":"Big Slow","Value":1,"StarSystem":"Nowhere Known"}]}"""));
        var (_, _, page) = InWindow(game);

        Assert.Equal("2 bookmarks, your carrier and 1 stored ship are in systems you have not visited.", page.UnplacedLine);

        page.BookmarkSwitch.IsChecked = false;
        page.ShipSwitch.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Your carrier is in a system you have not visited.", page.UnplacedLine);
    }

    [AvaloniaFact]
    public void EachSwitchHidesItsMarkers()
    {
        var (_, _, page) = InWindow(Keeping());

        foreach (var (kind, system, box) in new[]
        {
            (GalaxyMapMarkKind.Bookmark, Bubble5, page.BookmarkSwitch),
            (GalaxyMapMarkKind.Carrier, Bubble9, page.CarrierSwitch),
            (GalaxyMapMarkKind.Ships, Bubble12, page.ShipSwitch),
        })
        {
            box.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(page.Map.MarkScreenOf(kind, system));

            box.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(page.Map.MarkScreenOf(kind, system));
        }

        var engineer = BubbleEngineer();
        page.Map.View(GalaxyViewport.Around(engineer.Position!.Value));

        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Engineer, engineer.System!));

        page.EngineerSwitch.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(page.Map.MarkScreenOf(GalaxyMapMarkKind.Engineer, engineer.System!));
    }

    [AvaloniaFact]
    public void EngineersDrawAtSixThousandFourHundredLightYearsAndCloserAndTheLegendSaysSo()
    {
        var (_, _, page) = InWindow(Keeping());
        var engineer = BubbleEngineer();

        page.Map.View(GalaxyViewport.Around(engineer.Position!.Value) with { LightYearsAcross = 6_400 });
        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Engineer, engineer.System!));
        Assert.False(page.Map.Drawn.EngineersHidden);

        page.Map.View(page.Map.Viewport.ZoomOut());
        Assert.Null(page.Map.MarkScreenOf(GalaxyMapMarkKind.Engineer, engineer.System!));
        Assert.True(page.Map.Drawn.EngineersHidden);
        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Carrier, Bubble9));
    }

    [AvaloniaFact]
    public void MarksInOneSystemAreOneRowAndOneTargetNamingEveryKind()
    {
        var game = Played();
        game.Bookmarks.Add("F1", "Here too", Bubble9, Now);
        KeepCarrierIn(game, Bubble9);
        var (window, _, page) = InWindow(game);

        var bookmark = page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble9)!.Value;
        var carrier = page.Map.MarkScreenOf(GalaxyMapMarkKind.Carrier, Bubble9)!.Value;

        Assert.Equal(14, carrier.X - bookmark.X, 6);
        Assert.Equal(bookmark.Y, carrier.Y, 6);

        TapMapInWindow(window, page, bookmark);

        Assert.Equal(["Bookmark: Here too", "Your carrier: BNH-T2F"], page.StripKinds);
    }

    [AvaloniaFact]
    public void AnotherCommandersCarrierIsNotDrawnAndItsSwitchIsDisabled()
    {
        var game = Keeping();
        var (_, _, page) = InWindow(game);

        Assert.True(page.CarrierSwitch.IsEnabled);

        game.OffDuty = "Jameson";
        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.CarrierSwitch.IsEnabled);
        Assert.Null(page.Map.MarkScreenOf(GalaxyMapMarkKind.Carrier, Bubble9));
        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble5));
    }

    [AvaloniaFact]
    public void ABookmarkMadeFromTheStripAppearsAtOnceAndTheReplyKeepsTheSecondLine()
    {
        var game = Played();
        var (window, _, page) = InWindow(game);

        TapMapInWindow(window, page, MapPoint(page, Bubble5));
        Assert.Null(page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble5));

        Tile(page, GalaxyMapPage.Bookmark).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(page.Map.MarkScreenOf(GalaxyMapMarkKind.Bookmark, Bubble5));
        Assert.Equal($"Bookmarked {Bubble5}.", page.StripReply);
        Assert.Empty(page.StripKinds);
    }
}
