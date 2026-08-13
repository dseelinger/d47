using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using D47.App.Windowing;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The two desktop items in Phase 9: the window opens at a size that fits, and the panel
/// zooms the way a browser does. Driven through the real window rather than the helper,
/// because the part worth pinning is that the gestures survive the controls underneath them —
/// the transcript's scroll viewer wants the wheel and the ask box wants the keys.
/// </summary>
public class DesktopWindowTests
{
    [AvaloniaFact]
    public void ZoomWrapsTheWindowContentInALayoutTransformRatherThanARenderTransform()
    {
        var settings = TestSurface.Settings();
        var window = new Window { Content = new TextBlock { Text = "panel" }, Width = 400, Height = 300 };

        ZoomHost.Attach(window, settings);

        // Inside a horizontally scrolling viewport, which is what makes zoom browser-like:
        // enlarging past the window's width scrolls sideways rather than squeezing the layout
        // into what is left. Vertical scrolling is deliberately left to whatever inside the
        // window already does it.
        var viewport = Assert.IsType<ScrollViewer>(window.Content);

        Assert.Equal(ScrollBarVisibility.Auto, viewport.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, viewport.VerticalScrollBarVisibility);

        // A render transform would scale the finished picture and let the window clip it. A
        // layout transform re-runs measure and arrange, which is what makes text rewrap.
        Assert.IsType<LayoutTransformControl>(viewport.Content);
    }

    [AvaloniaFact]
    public void TheFourGesturesStepTheLadderAndPersist()
    {
        var settings = TestSurface.Settings();
        var window = new Window { Content = new TextBlock(), Width = 400, Height = 300 };
        var zoom = ZoomHost.Attach(window, settings);
        window.Show();

        Assert.Equal(100, zoom.Percent);

        window.KeyPress(Key.OemPlus, RawInputModifiers.Control, PhysicalKey.Equal, "+");
        Assert.Equal(110, zoom.Percent);

        window.KeyPress(Key.OemMinus, RawInputModifiers.Control, PhysicalKey.Minus, "-");
        Assert.Equal(100, zoom.Percent);

        zoom.Set(175);
        window.KeyPress(Key.D0, RawInputModifiers.Control, PhysicalKey.Digit0, "0");
        Assert.Equal(100, zoom.Percent);

        // Persisted like the theme, not held in the window: the level has to survive a restart
        // and be the same one the settings surface shows.
        zoom.Set(150);
        Assert.Equal("150", settings.Read("ui.zoom"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheWheelOnlyZoomsWithControlHeld()
    {
        var settings = TestSurface.Settings();
        var window = new Window { Content = new TextBlock(), Width = 400, Height = 300 };
        var zoom = ZoomHost.Attach(window, settings);
        window.Show();

        window.MouseWheel(new Point(50, 50), new Vector(0, 1));
        Assert.Equal(100, zoom.Percent);

        window.MouseWheel(new Point(50, 50), new Vector(0, 1), RawInputModifiers.Control);
        Assert.Equal(110, zoom.Percent);

        window.MouseWheel(new Point(50, 50), new Vector(0, -1), RawInputModifiers.Control);
        Assert.Equal(100, zoom.Percent);

        window.Close();
    }

    /// <summary>
    /// The ladder is a closed vocabulary, so a level that is not on it is refused at the row
    /// like any other bad choice. Snapping is what happens to a value that got into the
    /// settings file behind the row's back — a hand edit — and it happens where the level is
    /// read rather than where it is written.
    /// </summary>
    [AvaloniaFact]
    public void AnOffLadderLevelIsRefusedByTheRowAndSnappedWhenItIsRead()
    {
        var settings = TestSurface.Settings();

        var refused = settings.Apply("ui.zoom", "137", SettingsCaller.Panel);
        Assert.Equal(SettingApplyStatus.Rejected, refused.Status);

        var window = new Window { Content = new TextBlock(), Width = 400, Height = 300 };
        var zoom = ZoomHost.Attach(window, settings);

        Assert.Equal(125, ZoomLadder.Snap(137));
        Assert.Equal(100, zoom.Percent);
    }

    [AvaloniaFact]
    public void ARememberedPlacementIsAppliedBeforeTheWindowIsShown()
    {
        var (settings, viewState, _) = TestSurface.Create();
        _ = settings;

        viewState.Save(new ViewState
        {
            MainWindow = new WindowPlacement { Width = 500, Height = 400 },
        });

        var window = new Window { Width = 820, Height = 640 };
        WindowPlacementMemory.Attach(window, viewState);

        // Not 820x640: the remembered size wins, and it was applied while the window was still
        // unshown so there is no visible resize for the Commander to see.
        Assert.Equal(500, window.Width);
        Assert.Equal(400, window.Height);
    }

    [AvaloniaFact]
    public void ClosingTheWindowRemembersWhereItWasLeft()
    {
        var (_, viewState, _) = TestSurface.Create();

        var window = new Window { Width = 820, Height = 640 };
        WindowPlacementMemory.Attach(window, viewState);
        window.Show();

        window.Width = 600;
        window.Height = 500;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.Close();

        var remembered = viewState.Load().MainWindow;
        Assert.NotNull(remembered);
        Assert.Equal(600, remembered.Width);
        Assert.Equal(500, remembered.Height);
    }

    /// <summary>
    /// Through the real entry point, because the memory is worth nothing if the thing that
    /// opens the window does not attach it — which is exactly what was wrong: the settings
    /// window asked for its declared size on every launch, clamped by nothing.
    /// </summary>
    [AvaloniaFact]
    public void TheSettingsWindowRemembersItsSizeThroughTheWayItIsActuallyOpened()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new D47.App.Theming.ThemeManager(
            Avalonia.Application.Current!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .FollowSettings(settings);

        var owner = new Window { Width = 400, Height = 300 };
        owner.Show();

        D47.App.Settings.SettingsWindow.Show(owner, settings, viewState, paths);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var panel = owner.OwnedWindows.OfType<D47.App.Settings.SettingsWindow>().Single();

        panel.Width = 1024;
        panel.Height = 768;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        panel.Close();

        var remembered = viewState.Load().SettingsWindow;

        Assert.NotNull(remembered);
        Assert.Equal(1024, remembered.Width);
        Assert.Equal(768, remembered.Height);

        owner.Close();
    }

    /// <summary>
    /// The settings window remembers separately. Sharing the main window's numbers would mean
    /// resizing one silently resized the other, and they are different shapes with different
    /// content.
    /// </summary>
    [AvaloniaFact]
    public void TheTwoWindowsRememberTheirOwnSizes()
    {
        var (_, viewState, _) = TestSurface.Create();

        var main = new Window { Width = 820, Height = 640 };
        WindowPlacementMemory.Attach(main, viewState, WindowSlot.Main);
        main.Show();
        main.Width = 600;
        main.Height = 500;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        main.Close();

        var panel = new Window { Width = 1180, Height = 880 };
        WindowPlacementMemory.Attach(panel, viewState, WindowSlot.Settings);
        panel.Show();
        panel.Width = 1000;
        panel.Height = 900;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        panel.Close();

        var state = viewState.Load();

        Assert.Equal(600, state.MainWindow!.Width);
        Assert.Equal(500, state.MainWindow.Height);
        Assert.Equal(1000, state.SettingsWindow!.Width);
        Assert.Equal(900, state.SettingsWindow.Height);
    }

    /// <summary>
    /// Maximised has to survive too, and it is the state a Commander is most likely to leave a
    /// settings window in.
    /// </summary>
    [AvaloniaFact]
    public void ClosingMaximisedRemembersMaximised()
    {
        var (_, viewState, _) = TestSurface.Create();

        var window = new Window { Width = 1180, Height = 880 };
        WindowPlacementMemory.Attach(window, viewState, WindowSlot.Settings);
        window.Show();

        window.WindowState = WindowState.Maximized;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();

        var remembered = viewState.Load().SettingsWindow;

        Assert.NotNull(remembered);
        Assert.True(remembered.Maximized);

        // And the normal-state size it had before is what it restores to, not the maximised
        // rectangle — otherwise maximising once leaves a window that cannot be un-maximised
        // back to a size the Commander chose.
        Assert.Equal(1180, remembered.Width);
        Assert.Equal(880, remembered.Height);
    }

    /// <summary>
    /// Zoom scrolls the window horizontally rather than reflowing the layout, so at 125% the
    /// content is a quarter wider than the default was chosen for and clips on every launch.
    /// The default follows the zoom; a size the Commander chose does not.
    /// </summary>
    [AvaloniaFact]
    public void TheDefaultSizeFollowsTheZoomButARememberedOneDoesNot()
    {
        var (_, viewState, _) = TestSurface.Create();

        var fresh = new Window { Width = 800, Height = 600 };
        WindowPlacementMemory.Attach(fresh, viewState, WindowSlot.Settings, () => 125);

        Assert.Equal(1000, fresh.Width);
        Assert.Equal(750, fresh.Height);

        viewState.Save(viewState.Load() with
        {
            SettingsWindow = new WindowPlacement { Width = 900, Height = 700 },
        });

        var chosen = new Window { Width = 800, Height = 600 };
        WindowPlacementMemory.Attach(chosen, viewState, WindowSlot.Settings, () => 125);

        Assert.Equal(900, chosen.Width);
        Assert.Equal(700, chosen.Height);
    }

    /// <summary>
    /// One capture per interesting level, for a human to look at. The claim being checked by
    /// eye is that spacing scales with the text rather than the text growing inside a layout
    /// that stayed put.
    /// </summary>
    [AvaloniaFact]
    public void ZoomLevelsRenderToCaptures()
    {
        var settings = TestSurface.Settings();

        new D47.App.Theming.ThemeManager(
                Application.Current!,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(settings.Current.Ui.Theme);

        var window = new MainWindow(host: null);
        var zoom = ZoomHost.Attach(window, settings);
        window.Show();

        var output = TestSurface.CaptureDirectory;

        foreach (var level in new[] { 50, 100, 150, 200 })
        {
            zoom.Set(level);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(output, $"main-window-zoom-{level}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        Assert.NotEmpty(ZoomLadder.Steps);
        window.Close();
    }
}
