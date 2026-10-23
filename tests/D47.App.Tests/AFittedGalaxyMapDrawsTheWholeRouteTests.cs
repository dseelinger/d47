using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A route map is a <see cref="GalaxyMapView"/> fitted to the route, drawing every stop at any width (#1532).</summary>
public class AFittedGalaxyMapDrawsTheWholeRouteTests
{
    private static RouteMapStop Stop(string name, double x, double z = 0, RouteStopMarks marks = RouteStopMarks.None) =>
        new(name, new StarPosition(x, 0, z), marks);

    private static RouteMapStop NoPosition(string name) => new(name, null, RouteStopMarks.None);

    /// <summary>Three stops 10 ly apart along X: at 20, 180 and 340 px across a 360 px square, 180 px down.</summary>
    private static RouteMapStop[] Straight() => [Stop("A", 0), Stop("B", 10), Stop("C", 20)];

    private static (RouteMapView Map, Window Window) Open(IReadOnlyList<RouteMapStop> stops, int reached, string? here = null)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var map = new RouteMapView { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        map.Show(stops, reached, null, here);

        var window = new Window { Content = map, Width = 1000, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (map, window);
    }

    private static Color Ink(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    /// <summary>Which of the background, Grey and A the window draws at <paramref name="at"/> on the map, by nearest colour.</summary>
    private static string InkAt(RouteMapView map, Window window, Point at)
    {
        var onWindow = map.TranslatePoint(at, window)!.Value;

        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();

        var offset = ((int)onWindow.Y * buffer.RowBytes) + ((int)onWindow.X * 4);
        var bytes = new byte[4];
        Marshal.Copy(buffer.Address + offset, bytes, 0, 4);

        var drawn = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
            ? Color.FromRgb(bytes[2], bytes[1], bytes[0])
            : Color.FromRgb(bytes[0], bytes[1], bytes[2]);

        static int Apart(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

        return new[] { ThemeManager.BgKey, ThemeManager.GreyKey, ThemeManager.AKey }.MinBy(key => Apart(Ink(key), drawn))!;
    }

    private static (string Behind, string Ahead) Legs(IReadOnlyList<RouteMapStop> stops, int reached)
    {
        var (map, window) = Open(stops, reached);

        return (InkAt(map, window, new Point(100, 180)), InkAt(map, window, new Point(260, 180)));
    }

    [AvaloniaFact]
    public void ARouteTwentyThousandLightYearsWideStillDrawsItsStopsAndStars()
    {
        var stops = new[]
        {
            Stop("Start", 0),
            Stop("Neutron", 5_000, 400, RouteStopMarks.Neutron),
            Stop("Dwarf", 12_000, -300, RouteStopMarks.WhiteDwarf),
            Stop("End", 20_000),
        };

        var (map, _) = Open(stops, -1);
        var drawn = map.Map.Drawn;

        Assert.True(drawn.Route);
        Assert.True(drawn.Start);
        Assert.True(drawn.Destination);
        Assert.False(drawn.StopsHidden);
        Assert.Equal([RouteStar.Neutron, RouteStar.WhiteDwarf], drawn.Stars);
    }

    [AvaloniaFact]
    public void AStraightRouteEndsTwentyPixelsInFromEachSide()
    {
        var (map, _) = Open([Stop("A", 0), Stop("B", 12), Stop("C", 30)], -1);

        Assert.Equal(RouteMapView.LargestSide, map.Map.Bounds.Width);
        Assert.Equal(RouteMapView.LargestSide, map.Map.Bounds.Height);
        Assert.Equal(20, map.Map.ScreenOf("A")!.Value.X, 6);
        Assert.Equal(RouteMapView.LargestSide - 20, map.Map.ScreenOf("C")!.Value.X, 6);
    }

    [AvaloniaFact]
    public void TheLineIsGreyUpToTheStopReachedAndAmberAfterIt()
    {
        Assert.Equal((ThemeManager.GreyKey, ThemeManager.AKey), Legs(Straight(), 1));
        Assert.Equal((ThemeManager.GreyKey, ThemeManager.GreyKey), Legs(Straight(), 2));
        Assert.Equal((ThemeManager.AKey, ThemeManager.AKey), Legs(Straight(), -1));
    }

    [AvaloniaFact]
    public void AStopReachedWithoutAPositionLeavesTheWholeLineAmber()
    {
        RouteMapStop[] stops = [Stop("A", 0), NoPosition("X"), Stop("B", 10), Stop("C", 20)];

        Assert.Equal((ThemeManager.AKey, ThemeManager.AKey), Legs(stops, 1));
    }

    [AvaloniaFact]
    public void TheRouteMapDrawsNoScaleBar()
    {
        var (map, window) = Open(Straight(), -1);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-map-fitted.png");

        Assert.Equal(ThemeManager.BgKey, InkAt(map, window, new Point(12.5, RouteMapView.LargestSide - 12.5)));
    }
}
