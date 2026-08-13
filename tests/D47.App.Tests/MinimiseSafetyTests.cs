using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// "Keep working when the main window is minimized" (list.md Phase 9). Structural rather than
/// defended — but structural claims are exactly the ones worth an assertion, because nothing
/// about the code says out loud that it does not depend on a window state.
/// </summary>
public class MinimiseSafetyTests
{
    [AvaloniaFact]
    public void TheHeadsetPanelStillRendersWithTheMainWindowMinimised()
    {
        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        var window = new MainWindow(host: null);
        window.Show();

        using var headset = new VrPanelSurface(model, settings, _ => null);
        model.Append("Fixture One, docked.");

        var before = Pixels(headset);

        window.WindowState = WindowState.Minimized;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        model.Append("\nStill talking with the window down.");
        var after = Pixels(headset);

        Assert.Equal(WindowState.Minimized, window.WindowState);

        // Rendered, and rendered something different — a surface that returned the same bytes
        // would pass a "did it draw" assertion while being frozen on its last frame.
        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPanelStillRendersWithNoMainWindowAtAll()
    {
        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        // Closed, not merely minimised. Nothing in the headset path holds a reference to the
        // Commander's window, so there is nothing for its absence to break.
        var window = new MainWindow(host: null);
        window.Show();
        window.Close();

        using var headset = new VrPanelSurface(model, settings, _ => null);
        model.Append("Fixture Anchorage, 12.4 ly.");

        Assert.NotEmpty(Pixels(headset));
    }

    /// <summary>
    /// Drives the real draw path, into a buffer shaped like the mapped staging texture the
    /// production code hands it.
    /// </summary>
    private static byte[] Pixels(VrPanelSurface surface)
    {
        var (width, height) = surface.Size;
        var rowBytes = width * 4;
        var buffer = new byte[rowBytes * height];

        unsafe
        {
            fixed (byte* into = buffer)
            {
                surface.Draw((IntPtr)into, rowBytes);
            }
        }

        return buffer;
    }
}
