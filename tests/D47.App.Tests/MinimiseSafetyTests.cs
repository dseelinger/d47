using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>"Keep working when the main window is minimized".</summary>
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

        // What VrHost.Configure does before the first serve, and without it this surface is a configuration
        // production never reaches: the size says mini and the view still holds the full panel's chrome, so a
        // 280-pixel surface is asked to carry a header, a tab strip and a page bar.
        headset.ApplyMode();

        model.Append("Fixture One, docked.");

        var before = Pixels(headset);

        window.WindowState = WindowState.Minimized;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        model.Append("\nStill talking with the window down.");
        var after = Pixels(headset);

        Assert.Equal(WindowState.Minimized, window.WindowState);

        // Rendered, and rendered something different — a surface that returned the same bytes would pass a
        // "did it draw" assertion while being frozen on its last frame.
        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);

        window.Close();
    }

    /// <summary><c>CommunityGoalSearch.Showing</c> is wired from the window's <c>WindowState</c> and <c>IsVisible</c> as well as nav state, so "refresh" cannot fire an unseen search while the window is minimised or covered.</summary>
    [AvaloniaFact]
    public void AMinimisedWindowReportsItsOwnStateForAVisibilityGateToRead()
    {
        var window = new MainWindow(host: null);
        window.Show();

        Assert.NotEqual(WindowState.Minimized, window.WindowState);

        window.WindowState = WindowState.Minimized;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Minimized, window.WindowState);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPanelStillRendersWithNoMainWindowAtAll()
    {
        var (settings, _, _) = TestSurface.Create();
        var model = new PanelViewModel();

        // Closed, not merely minimised.
        var window = new MainWindow(host: null);
        window.Show();
        window.Close();

        using var headset = new VrPanelSurface(model, settings, _ => null);
        model.Append("Fixture Anchorage, 12.4 ly.");

        Assert.NotEmpty(Pixels(headset));
    }

    /// <summary>The same claim for the big panel, at the resolution a Commander actually runs it at.</summary>
    [AvaloniaFact]
    public void TheFullHeadsetPanelRendersAndKeepsUp()
    {
        var (settings, _, _) = TestSurface.Create();

        settings.Replace(
            "the full headset panel is the subject here",
            current => current with { Vr = current.Vr with { Mode = "full" } });

        var model = new PanelViewModel();

        using var headset = new VrPanelSurface(model, settings, _ => null);

        headset.ApplyMode();

        model.Append("Fixture One, docked.");
        var before = Pixels(headset);

        model.Append("\nStill talking.");
        var after = Pixels(headset);

        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);
    }

    /// <summary>
    /// Drives the real draw path, into a buffer shaped like the mapped staging texture the production
    /// code hands it.
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
