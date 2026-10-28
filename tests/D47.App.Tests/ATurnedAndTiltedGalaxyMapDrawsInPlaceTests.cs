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

/// <summary>The galaxy map draws its marks, legs, borders and ground in place at any heading and tilt (#1557).</summary>
public class ATurnedAndTiltedGalaxyMapDrawsInPlaceTests
{
    private const double Side = 600;

    private static readonly StarPosition SagittariusA = new(25.2, 0, 25_900);

    private static (GalaxyMapView Map, Window Window) Open(GalaxyMapScene scene, GalaxyViewport viewport)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var map = new GalaxyMapView { Width = Side, Height = Side, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        map.Show(scene);
        map.View(viewport);

        var window = new Window { Content = map, Width = Side, Height = Side };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (map, window);
    }

    private static void Turn(GalaxyMapView map, GalaxyViewport viewport)
    {
        map.View(viewport);
        Dispatcher.UIThread.RunJobs();
    }

    private static Color Ink(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static int Apart(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    /// <summary>The colours drawn in the 3 px square on <paramref name="at"/>.</summary>
    private static List<Color> Around(Window window, Point at)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();

        var colours = new List<Color>();
        var bytes = new byte[4];

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var offset = (((int)at.Y + dy) * buffer.RowBytes) + (((int)at.X + dx) * 4);
                Marshal.Copy(buffer.Address + offset, bytes, 0, 4);

                colours.Add(buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
                    ? Color.FromRgb(bytes[2], bytes[1], bytes[0])
                    : Color.FromRgb(bytes[0], bytes[1], bytes[2]));
            }
        }

        return colours;
    }

    /// <summary>Which of the background, the ground and the border colour is drawn at <paramref name="at"/>, by nearest colour.</summary>
    private static string InkAt(Window window, Point at)
    {
        var drawn = Around(window, at)[4];

        return new[] { ThemeManager.BgKey, ThemeManager.MapGroundKey, ThemeManager.Line2Key }.MinBy(key => Apart(Ink(key), drawn))!;
    }

    private static Point Screen(GalaxyViewport viewport, StarPosition at)
    {
        var (x, y) = viewport.ToScreen(at, Side, Side);

        return new Point(x, y);
    }

    /// <summary>The last point inside a region going out from Sagittarius A* towards -X, in 50 ly steps.</summary>
    private static StarPosition Rim()
    {
        var at = SagittariusA;

        while (GalacticRegions.Find(at with { X = at.X - 50 }) is not null)
        {
            at = at with { X = at.X - 50 };
        }

        return at;
    }

    [AvaloniaFact]
    public void TwoSystemsAtOneXAndZDrawAsOneAtRestAndTwoAtTiltNinety()
    {
        var low = new StarPosition(100, 0, 200);
        var high = new StarPosition(100, 500, 200);
        var scene = new GalaxyMapScene(
            false,
            new TravelPicture([[low, high]], [low, high], 1),
            null,
            -1,
            null,
            null,
            Visited: [new GalaxyMapStar("Low", low), new GalaxyMapStar("High", high)]);
        var atRest = GalaxyViewport.Around(low).ZoomOut();

        var (map, _) = Open(scene, atRest);

        Assert.Equal(map.ScreenOf("Low"), map.ScreenOf("High"));

        Turn(map, atRest with { Tilt = 90 });

        var lowAt = map.ScreenOf("Low")!.Value;
        var highAt = map.ScreenOf("High")!.Value;

        Assert.Equal(lowAt.X, highAt.X, 6);
        Assert.Equal(500 * Side / atRest.LightYearsAcross, lowAt.Y - highAt.Y, 6);
    }

    [AvaloniaFact]
    public void AroundACommanderAboveThePlaneDrawsYouAtTheCentreTurnedAndTilted()
    {
        var commander = new StarPosition(1_000, 1_000, -2_000);
        var scene = new GalaxyMapScene(true, null, null, -1, commander, "High Above");

        var (map, _) = Open(scene, GalaxyViewport.Around(commander) with { Heading = 30, Tilt = 45 });

        Assert.True(map.Drawn.You);
        Assert.Equal(Side / 2, map.YouAt!.Value.X, 6);
        Assert.Equal(Side / 2, map.YouAt!.Value.Y, 6);
    }

    [AvaloniaFact]
    public void TheGroundIsThePlaneSeenAtTiltFortyFiveAndIsNotDrawnAtTiltNinety()
    {
        var rim = Rim();
        var inside = rim with { X = rim.X + 400 };
        var outside = rim with { X = rim.X - 400 };

        Assert.NotNull(GalacticRegions.Find(inside));
        Assert.Null(GalacticRegions.Find(outside));

        var tilted = new GalaxyViewport(rim.X, rim.Z, 6_400) { Heading = 30, Tilt = 45 };
        var (_, window) = Open(GalaxyMapScene.Empty, tilted);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("galaxy-map-tilted-45.png");
        }

        Assert.Equal(ThemeManager.MapGroundKey, InkAt(window, Screen(tilted, inside)));
        Assert.Equal(ThemeManager.BgKey, InkAt(window, Screen(tilted, outside)));

        var edgeOn = tilted with { Tilt = 90 };
        var map = (GalaxyMapView)window.Content!;
        Turn(map, edgeOn);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("galaxy-map-tilted-90.png");
        }

        // The plane line (#1559) passes through this point at tilt 90, so it is not Bg; it must not be ground.
        Assert.NotEqual(ThemeManager.MapGroundKey, InkAt(window, Screen(edgeOn, inside)));
    }

    [AvaloniaFact]
    public void ALegIsRedrawnWhereItsEndsAreOnceTheMapTurns()
    {
        var from = new StarPosition(0, 0, 0);
        var to = new StarPosition(400, 300, -200);
        var scene = new GalaxyMapScene(false, new TravelPicture([[from, to]], [from, to], 1), null, -1, null, null);
        var atRest = GalaxyViewport.Around(from).ZoomOut();

        var (map, window) = Open(scene, atRest);

        var turned = atRest with { Heading = 30, Tilt = 45 };
        Turn(map, turned);

        var a = Screen(turned, from);
        var b = Screen(turned, to);
        var middle = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var grey2 = Ink(ThemeManager.Grey2Key);

        Assert.Contains(Around(window, middle), drawn => Apart(drawn, grey2) < Apart(drawn, Ink(ThemeManager.BgKey)));
    }
}
