using System.Text.Json;
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
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The mini panel's plan list puts the Commander's stop at the top (#2772).</summary>
public class TheMiniPanelPlanScrollsToTheCommandersStopTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private const int StopCount = 13;

    private sealed class Game
    {
        public string? Here { get; set; }

        public RoutePlanBook Plans { get; } = new("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);

        private int _minute;

        public void JumpTo(string system)
        {
            _minute++;
            Here = system;

            var json = JsonSerializer.Serialize(new { @event = "FSDJump", StarSystem = system });
            var root = JsonDocument.Parse(json).RootElement;

            Plans.Apply([new JournalEvent(Later.AddMinutes(_minute), "FSDJump", root)]);
        }
    }

    private static string Stop(int index) => $"Stop {index}";

    private static PlottedRoute Jump() => new(
        "Origin",
        Stop(StopCount - 1),
        StopCount * 400,
        StopCount,
        [.. Enumerable.Range(0, StopCount).Select(index =>
            new RouteWaypoint(Stop(index), 1, (StopCount - 1 - index) * 400.0, IsNeutron: index < StopCount - 1))]);

    private static RichesRoute Riches() =>
        new([.. Enumerable.Range(0, StopCount).Select(index => new RichesStop(Stop(index), 2, [])
        {
            Position = new StarPosition(index * 100, 0, 0),
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
            () => NavRoute.None,
            () => game.Here,
            CapabilityRegistry.Build([]),
            game.Plans,
            Settings: TestSurface.Settings())
        {
            Now = () => Now,
        });

        panel.Tab = PanelTab.Astrometrics;
        panel.Nav.SelectRoot(PanelTab.Astrometrics, RoutingPages.PlanRoot);
        panel.Mode = PanelMode.Mini;

        return panel;
    }

    /// <summary>The headset's mini panel: laid out offscreen at mini's size, settled the way the headset settles it.</summary>
    private sealed class Headset : IDisposable
    {
        private readonly OffscreenSurface _surface;

        public Headset(Game game)
        {
            View = Panel(game);
            _surface = new OffscreenSurface(View, new PixelSize(PanelResolution.Mini.Width, PanelResolution.Mini.Height));
        }

        public PanelView View { get; }

        /// <summary>Lays out and rasterises; the bitmap belongs to the surface.</summary>
        public Avalonia.Media.Imaging.RenderTargetBitmap Draw()
        {
            Dispatcher.UIThread.RunJobs();
            return _surface.Render(View.KeepUp);
        }

        public void Dispose() => _surface.Dispose();
    }

    private static RouteMini Card(Control panel) =>
        panel.GetVisualDescendants().OfType<RouteMini>().Single();

    private static ScrollViewer Scroller(Control panel) =>
        Card(panel).GetVisualDescendants().OfType<ScrollViewer>().First();

    /// <summary>How far below the top of the list's viewport the stop's name is drawn.</summary>
    private static double TopOf(Control panel, string system)
    {
        var scroller = Scroller(panel);
        var name = Card(panel).GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == system);

        return name.TranslatePoint(default, scroller)!.Value.Y;
    }

    /// <summary>The stop whose name is drawn nearest the top of the viewport without being above it.</summary>
    private static string TopStop(Control panel) =>
        Enumerable.Range(0, StopCount)
            .Select(Stop)
            .Where(system => TopOf(panel, system) >= 0)
            .OrderBy(system => TopOf(panel, system))
            .First();

    private static void AssertAtTheTop(Control panel, string system)
    {
        Assert.Equal(system, TopStop(panel));
        Assert.InRange(TopOf(panel, system), 0, 12);
    }

    [AvaloniaFact]
    public void TheCurrentSystemIsTheTopRowOnTheHeadset()
    {
        var game = new Game();
        using var headset = new Headset(game);

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        game.JumpTo(Stop(6));

        var frame = headset.Draw();
        frame.SaveCapture("route-mini-anchored-headset.png");

        AssertAtTheTop(headset.View, Stop(6));
        var name = Card(headset.View).GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == Stop(6));
        Assert.Equal(Application.Current!.Resources[ThemeManager.CyanKey], name.Foreground);

        var below = TopOf(headset.View, Stop(7));
        Assert.True(below > TopOf(headset.View, Stop(6)), "the next stop is the row below");
        Assert.True(below < Scroller(headset.View).Viewport.Height, "the next stop is on screen");
    }

    [AvaloniaFact]
    public void AJumpToTheNextStopMovesTheListDownOneRow()
    {
        var game = new Game();
        using var headset = new Headset(game);

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        game.JumpTo(Stop(6));
        headset.Draw();

        game.JumpTo(Stop(7));
        headset.Draw();

        AssertAtTheTop(headset.View, Stop(7));
    }

    [AvaloniaFact]
    public void BetweenStopsTheLastStopReachedIsTheTopRow()
    {
        var game = new Game();
        using var headset = new Headset(game);

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        game.JumpTo(Stop(1));
        headset.Draw();
        game.Here = "Somewhere Between";
        Card(headset.View).Refresh();
        headset.Draw();

        AssertAtTheTop(headset.View, Stop(1));
    }

    [AvaloniaFact]
    public void WithNothingReachedTheListStaysAtTheTop()
    {
        var game = new Game { Here = "Origin" };
        using var headset = new Headset(game);

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        headset.Draw();

        Assert.Equal(0, Scroller(headset.View).Offset.Y);
    }

    [AvaloniaFact]
    public void AtTheLastStopTheLastStopIsOnScreen()
    {
        var game = new Game();
        using var headset = new Headset(game);

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        game.JumpTo(Stop(StopCount - 1));
        headset.Draw();

        var scroller = Scroller(headset.View);
        var last = TopOf(headset.View, Stop(StopCount - 1));

        Assert.True(scroller.Offset.Y > 0, "the list scrolled");
        Assert.InRange(last, 0, scroller.Viewport.Height - 12);
    }

    [AvaloniaFact]
    public void ARedrawThatKeepsTheStopKeepsTheCommandersScroll()
    {
        var game = new Game();
        using var headset = new Headset(game);

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        game.JumpTo(Stop(6));
        headset.Draw();

        Scroller(headset.View).Offset = default;
        Card(headset.View).Refresh();
        headset.Draw();

        Assert.Equal(0, Scroller(headset.View).Offset.Y);
    }

    [AvaloniaFact]
    public void ARichesPlanScrollsToTheLastStopReached()
    {
        var game = new Game();
        using var headset = new Headset(game);

        game.Plans.Record(Riches(), "Road to Riches", Later);
        game.JumpTo(Stop(4));
        headset.Draw();
        game.Here = "Somewhere Between";
        Card(headset.View).Refresh();
        headset.Draw();

        AssertAtTheTop(headset.View, Stop(4));
    }

    [AvaloniaTheory]
    [InlineData(RoutePlanKind.Jump)]
    [InlineData(RoutePlanKind.Riches)]
    [InlineData(RoutePlanKind.Exobiology)]
    [InlineData(RoutePlanKind.Carrier)]
    public void EveryPlanKindScrollsToTheCurrentSystem(RoutePlanKind kind)
    {
        var game = new Game();
        using var headset = new Headset(game);

        switch (kind)
        {
            case RoutePlanKind.Jump:
                game.Plans.Record(Jump(), "Origin to Stop 12", Later);
                break;
            case RoutePlanKind.Riches:
                game.Plans.Record(Riches(), "Road to Riches", Later);
                break;
            case RoutePlanKind.Exobiology:
                game.Plans.Record(
                    new ExobiologyRoute([.. Enumerable.Range(0, StopCount).Select(index => new ExobiologyStop(Stop(index), 2, []))]),
                    "Exobiology",
                    Later);
                break;
            case RoutePlanKind.Carrier:
                game.Plans.Record(
                    new CarrierRoute([.. Enumerable.Range(0, StopCount).Select(index =>
                        new CarrierWaypoint(Stop(index), index * 400, (StopCount - 1 - index) * 400, 1000, 100, false, 0, 0, false, false, index == StopCount - 1))]),
                    carrierId: 1,
                    "Carrier",
                    Later);
                break;
        }

        headset.Draw();
        game.Here = Stop(6);
        Card(headset.View).Refresh();
        headset.Draw();

        AssertAtTheTop(headset.View, Stop(6));
    }

    [AvaloniaFact]
    public void TheFlatWindowsMiniModeScrollsToTheCurrentSystem()
    {
        var game = new Game();
        var panel = Panel(game);

        var window = new Window
        {
            Content = panel,
            Width = PanelResolution.Mini.Width,
            Height = PanelResolution.Mini.Height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();

        game.Plans.Record(Jump(), "Origin to Stop 12", Later);
        game.JumpTo(Stop(8));
        Dispatcher.UIThread.RunJobs();

        AssertAtTheTop(panel, Stop(8));
        window.SaveFrame("route-mini-anchored-window.png");
    }
}
