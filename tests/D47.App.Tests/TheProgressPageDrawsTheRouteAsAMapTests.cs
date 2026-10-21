using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Progress page draws the plotted route as a map beside the hop list (#1396).</summary>
public class TheProgressPageDrawsTheRouteAsAMapTests
{
    private static NavRoute Route(int hops = 40, bool positioned = true) => new()
    {
        Hops =
        [
            .. Enumerable.Range(0, hops).Select(index => new RouteHop($"System {index}", index % 7 == 3 ? "N" : index % 11 == 5 ? "D" : "G")
            {
                Position = positioned ? (index * 40.0, 0, 300 * Math.Sin(index / 5.0)) : null,
            }),
        ],
    };

    private static (RouteProgressPage Page, Window Window) Open(
        Func<NavRoute> route,
        Func<string?> here,
        Func<string, string?>? offDuty = null,
        Func<StarPosition?>? position = null,
        double width = 1000,
        double height = 700)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var page = new RouteProgressPage(route, here, null, offDuty, position);
        var window = new Window { Content = page, Width = width, Height = height, Background = (IBrush?)Application.Current.Resources[ThemeManager.BgKey] };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (page, window);
    }

    private static RouteMapView Map(Control page) => page.GetVisualDescendants().OfType<RouteMapView>().Single();

    [AvaloniaFact]
    public void APlottedRouteShowsTheMapAtItsLargestSideInTheWindow()
    {
        var (page, window) = Open(() => Route(), () => "System 12");

        var map = Map(page);

        Assert.True(map.IsVisible);
        Assert.Equal(RouteMapView.LargestSide, map.Bounds.Width);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-map-window.png");
    }

    [AvaloniaFact]
    public void ABodyLessTallThanTheMapKeepsItBetweenTheSmallestAndLargestSide()
    {
        var (page, window) = Open(() => Route(), () => "System 12", width: 1024, height: 640);

        var map = Map(page);

        Assert.InRange(map.Bounds.Width, RouteMapView.SmallestSide, RouteMapView.LargestSide);
        Assert.Equal(map.Bounds.Width + RouteMapView.KeyHeight, map.Bounds.Height);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-map-panel.png");
    }

    [AvaloniaFact]
    public void ACommanderOffTheRouteWithAPositionStillGetsTheMap()
    {
        var (page, window) = Open(() => Route(), () => "Elsewhere", position: () => new StarPosition(200, 0, 100));

        Assert.True(Map(page).IsVisible);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-map-off-route.png");
    }

    [AvaloniaFact]
    public void TheMapFollowsAJumpAndANewRoute()
    {
        var here = "System 3";
        var route = Route(20);
        var (page, _) = Open(() => route, () => here);
        var before = Map(page).Bounds;

        here = "System 15";
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Map(page).IsVisible);

        route = Route(1, positioned: true);
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.False(Map(page).IsVisible);
        Assert.NotEqual(default, before);

        route = Route(10);
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Map(page).IsVisible);
    }

    [AvaloniaFact]
    public void NoRouteAndFewerThanTwoPositionedHopsShowNoMap()
    {
        var (none, _) = Open(() => NavRoute.None, () => "Sol");
        var (unplaced, _) = Open(() => Route(10, positioned: false), () => "System 2");

        Assert.False(Map(none).IsVisible);
        Assert.False(Map(unplaced).IsVisible);
    }

    [AvaloniaFact]
    public void OffDutyShowsNoMap()
    {
        var (page, _) = Open(() => Route(), () => "System 2", offDuty: what => $"CMDR Kestrel Vane is in the game; their {what} shows when they are the Commander shown.");

        Assert.False(Map(page).IsVisible);
    }
}
