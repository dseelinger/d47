using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Galaxy map pans and zooms from the keyboard like its tiles (#1502).</summary>
public class TheGalaxyMapTakesKeysTests
{
    private static readonly StarPosition Wregoe = new(-40.6, 37.2, 75.3);

    private static GalaxyMapPage Opened(out Window window)
    {
        var shown = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Show();
        window = shown.Window;
        Dispatcher.UIThread.RunJobs();

        return shown.Page;
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void OpeningThePageFocusesTheMapAndRightPansLikeTheRightTile()
    {
        var page = Opened(out var window);
        Assert.True(page.Map.IsFocused);
        var before = page.Map.Viewport;

        Press(window, Key.Right, PhysicalKey.ArrowRight);

        Assert.Equal(before.Pan(1, 0), page.Map.Viewport);
        Assert.Equal(-1, page.Framing.SelectedIndex);
    }

    [AvaloniaFact]
    public void PlusAndMinusStepTheZoomAndStopAtTheEnds()
    {
        var page = Opened(out var window);
        var before = page.Map.Viewport;

        Press(window, Key.OemPlus, PhysicalKey.Equal);
        Assert.Equal(before.ZoomIn(), page.Map.Viewport);

        Press(window, Key.OemMinus, PhysicalKey.Minus);
        Assert.Equal(before, page.Map.Viewport);

        for (var step = 0; step < 30; step++)
        {
            Press(window, Key.OemMinus, PhysicalKey.Minus);
        }

        var widest = page.Map.Viewport;
        Press(window, Key.OemMinus, PhysicalKey.Minus);
        Assert.Equal(widest, page.Map.Viewport);
    }

    [AvaloniaFact]
    public void HomeIsTopDownAndLeavesTheFramingAlone()
    {
        var page = Opened(out var window);

        Press(window, Key.OemCloseBrackets, PhysicalKey.BracketRight);
        Assert.NotEqual(0, page.Map.Viewport.Heading);

        Press(window, Key.Home, PhysicalKey.Home);

        Assert.Equal(0, page.Map.Viewport.Heading);
        Assert.Equal(0, page.Map.Viewport.Tilt);
    }

    [AvaloniaFact]
    public void ControlPlusDoesNotZoomTheMap()
    {
        var page = Opened(out var window);
        var before = page.Map.Viewport;

        Press(window, Key.OemPlus, PhysicalKey.Equal, RawInputModifiers.Control);

        Assert.Equal(before, page.Map.Viewport);
    }

    [AvaloniaFact]
    public void ArrowsOnTheFramingSegmentDoNotPanTheMap()
    {
        var page = Opened(out var window);
        var before = page.Map.Viewport;
        var radio = page.Framing.GetVisualDescendants().OfType<RadioButton>().First();
        radio.Focus();

        Press(window, Key.Right, PhysicalKey.ArrowRight);

        Assert.NotEqual(before.Pan(1, 0), page.Map.Viewport);
        Assert.NotEqual(-1, page.Framing.SelectedIndex);
    }
}
