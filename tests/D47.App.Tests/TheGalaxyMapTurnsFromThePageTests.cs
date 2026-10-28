using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Galaxy map turns and tilts from its tiles, keys and mouse, and keeps the angle when it is framed (#1559).</summary>
public class TheGalaxyMapTurnsFromThePageTests
{
    private static Button Tile(GalaxyMapPage page, string says) => TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Tile(page, says);

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Drag(Window window, GalaxyMapPage page, MouseButton button, double dx, double dy)
    {
        var from = page.Map.TranslatePoint(new Point(page.Map.Bounds.Width / 2, 20), window)!.Value;

        window.MouseDown(from, button);
        window.MouseMove(from + new Point(dx / 2, dy / 2), RawInputModifiers.None);
        window.MouseMove(from + new Point(dx, dy), RawInputModifiers.None);
        window.MouseUp(from + new Point(dx, dy), button);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void EachTileOnThePanelMovesTheViewAsItsNameSays()
    {
        var (surface, panel, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.OnThePanel(TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Played());

        void Tap(string says) => TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Tap(surface, panel, Tile(page, says));

        var start = page.Map.Viewport;

        Assert.False(Tile(page, "Top down").IsEnabled);
        Assert.False(Tile(page, "Tilt down").IsEnabled);

        Tap("Turn clockwise");
        Assert.Equal(start.Turned(1), page.Map.Viewport);

        Tap("Turn anticlockwise");
        Tap("Turn anticlockwise");
        Assert.Equal(15, page.Map.Viewport.Heading);

        Tap("Tilt up");
        Assert.Equal(15, page.Map.Viewport.Tilt);
        Assert.True(Tile(page, "Tilt down").IsEnabled);

        Tap("Tilt down");
        Assert.Equal(0, page.Map.Viewport.Tilt);

        Tap("Tilt up");
        Tap("Top down");
        Assert.Equal(0, page.Map.Viewport.Heading);
        Assert.Equal(0, page.Map.Viewport.Tilt);
        Assert.False(Tile(page, "Top down").IsEnabled);
    }

    [AvaloniaFact]
    public void TiltUpStopsAtNinety()
    {
        var (window, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();

        for (var press = 0; press < 8; press++)
        {
            Press(window, Key.PageUp, PhysicalKey.PageUp);
        }

        Assert.Equal(90, page.Map.Viewport.Tilt);
        Assert.False(Tile(page, "Tilt up").IsEnabled);
    }

    [AvaloniaFact]
    public void TheBracketsPageKeysAndHomeTurnTiltAndClearTheView()
    {
        var (window, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();

        Press(window, Key.OemCloseBrackets, PhysicalKey.BracketRight);
        Assert.Equal(345, page.Map.Viewport.Heading);

        Press(window, Key.OemOpenBrackets, PhysicalKey.BracketLeft);
        Press(window, Key.OemOpenBrackets, PhysicalKey.BracketLeft);
        Assert.Equal(15, page.Map.Viewport.Heading);

        Press(window, Key.PageUp, PhysicalKey.PageUp);
        Press(window, Key.PageUp, PhysicalKey.PageUp);
        Press(window, Key.PageDown, PhysicalKey.PageDown);
        Assert.Equal(15, page.Map.Viewport.Tilt);

        Press(window, Key.Home, PhysicalKey.Home);
        Assert.Equal(0, page.Map.Viewport.Heading);
        Assert.Equal(0, page.Map.Viewport.Tilt);
    }

    [AvaloniaFact]
    public void ALeftDragTurnsTheMapAndSelectsNothing()
    {
        var (window, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();
        var before = page.Map.Viewport;

        Drag(window, page, MouseButton.Left, 75, 0);

        Assert.Equal(330, page.Map.Viewport.Heading, 6);
        Assert.Equal(0, page.Map.Viewport.Tilt, 6);
        Assert.Equal(before.CentreX, page.Map.Viewport.CentreX);
        Assert.Equal(before.CentreZ, page.Map.Viewport.CentreZ);
        Assert.Null(page.Map.Selected);
        Assert.False(page.StripShown);
    }

    [AvaloniaFact]
    public void ALeftDragUpTiltsTheMapFurther()
    {
        var (window, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();

        Drag(window, page, MouseButton.Left, 0, -50);

        Assert.Equal(20, page.Map.Viewport.Tilt, 6);
        Assert.Equal(0, page.Map.Viewport.Heading, 6);
    }

    [AvaloniaTheory]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    public void ARightOrMiddleDragPansTheMapAndSelectsNothing(MouseButton button)
    {
        var (window, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();
        var before = page.Map.Viewport;

        Drag(window, page, button, 75, 0);

        var expected = before.Dragged(75, 0, page.Map.Bounds.Width, page.Map.Bounds.Height);

        Assert.Equal(0, page.Map.Viewport.Heading);
        Assert.Equal(0, page.Map.Viewport.Tilt);
        Assert.Equal(expected.CentreX, page.Map.Viewport.CentreX, 6);
        Assert.Equal(expected.CentreZ, page.Map.Viewport.CentreZ, 6);
        Assert.Null(page.Map.Selected);
    }

    [AvaloniaFact]
    public void AnAngledViewKeepsItsAngleWhenAFramingIsChosen()
    {
        var (_, _, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();
        page.Map.View(page.Map.Viewport with { Heading = 45, Tilt = 30 });

        foreach (var framing in new[] { 0, 1, 2 })
        {
            page.Framing.SelectedIndex = -1;
            page.Framing.SelectedIndex = framing;

            Assert.Equal((45, 30), (page.Map.Viewport.Heading, page.Map.Viewport.Tilt));
        }
    }

    [AvaloniaFact]
    public void ThePanelShowsTheNewTilesTheReadoutAndThePlaneLine()
    {
        var (surface, panel, page) = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.OnThePanel(TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Played());

        Assert.NotNull(Tile(page, "Top down"));
        page.Map.View(page.Map.Viewport with { Heading = 330, Tilt = 30 });
        surface.Render().SaveCapture("galaxy-map-panel-turned.png");

        page.Map.View(page.Map.Viewport with { Tilt = 90 });
        surface.Render().SaveCapture("galaxy-map-panel-edge-on.png");
    }

    [Fact]
    public void TheAngleReadoutNamesHeadingAndTilt()
    {
        Assert.Equal("heading 330° · tilt 30°", GalaxyMapView.AngleWords(330, 30));
    }
}
