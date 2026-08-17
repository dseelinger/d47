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
    /// Maximised has to survive too, and it is a state a Commander leaves a window in and then
    /// expects back.
    /// </summary>
    [AvaloniaFact]
    public void ClosingMaximisedRemembersMaximised()
    {
        var (_, viewState, _) = TestSurface.Create();

        var window = new Window { Width = 1180, Height = 880 };
        WindowPlacementMemory.Attach(window, viewState);
        window.Show();

        // Whatever it opened at, which is a proportion of the screen rather than the numbers
        // above: this test is about what maximising does to the memory, not about the default.
        var normal = (window.Width, window.Height);

        window.WindowState = WindowState.Maximized;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();

        var remembered = viewState.Load().MainWindow;

        Assert.NotNull(remembered);
        Assert.True(remembered.Maximized);

        // And the normal-state size it had before is what it restores to, not the maximised
        // rectangle — otherwise maximising once leaves a window that cannot be un-maximised
        // back to a size the Commander chose.
        Assert.Equal(normal, (remembered.Width, remembered.Height));
    }

    /// <summary>
    /// And it comes back maximised, which is the half nothing asserted.
    /// </summary>
    [AvaloniaFact]
    public void ARememberedMaximisedWindowOpensMaximised()
    {
        var (_, viewState, _) = TestSurface.Create();

        viewState.Save(new ViewState
        {
            MainWindow = new WindowPlacement { Width = 500, Height = 400, Maximized = true },
        });

        var window = new Window { Width = 820, Height = 640 };
        WindowPlacementMemory.Attach(window, viewState);

        Assert.Equal(WindowState.Maximized, window.WindowState);

        // And the restored size is still the one to un-maximise back to.
        Assert.Equal(500, window.Width);
        Assert.Equal(400, window.Height);
    }

    /// <summary>
    /// The monitor a maximised window was on travels with the flag, so it comes back on that
    /// one (remediation.md, "Window state is not restored").
    /// <para>
    /// Its own pair of numbers, because <c>X</c> and <c>Y</c> hold the restored rectangle and
    /// have to keep holding it — maximised on the second monitor and restored on the first is an
    /// ordinary arrangement that one pair cannot describe.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void MaximisingRemembersWhichScreenItWasMaximisedOn()
    {
        var (_, viewState, _) = TestSurface.Create();

        var window = new Window { Width = 900, Height = 700 };
        WindowPlacementMemory.Attach(window, viewState);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.WindowState = WindowState.Maximized;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();

        var remembered = viewState.Load().MainWindow;

        Assert.NotNull(remembered);
        Assert.True(remembered.Maximized);

        var screen = window.Screens!.Primary ?? window.Screens.All[0];

        Assert.Equal(screen.WorkingArea.X, remembered.MaximizedOnX);
        Assert.Equal(screen.WorkingArea.Y, remembered.MaximizedOnY);
    }

    /// <summary>
    /// And it is put back there before it is maximised, because Windows maximises a window to
    /// whichever monitor it is already on.
    /// </summary>
    [AvaloniaFact]
    public void AMaximisedWindowIsPutBackOnItsOwnScreenFirst()
    {
        var (_, viewState, _) = TestSurface.Create();

        var window = new Window { Width = 900, Height = 700 };
        var screen = window.Screens!.Primary ?? window.Screens.All[0];

        viewState.Save(new ViewState
        {
            MainWindow = new WindowPlacement
            {
                Width = 500,
                Height = 400,
                Maximized = true,
                MaximizedOnX = screen.WorkingArea.X,
                MaximizedOnY = screen.WorkingArea.Y,
            },
        });

        WindowPlacementMemory.Attach(window, viewState);

        Assert.Equal(WindowStartupLocation.Manual, window.WindowStartupLocation);
        Assert.Equal(new PixelPoint(screen.WorkingArea.X, screen.WorkingArea.Y), window.Position);
        Assert.Equal(WindowState.Maximized, window.WindowState);
    }

    /// <summary>
    /// A monitor that has been unplugged since is not one to open on. The platform decides
    /// instead, which is the same fail-soft the restored position already has.
    /// </summary>
    [AvaloniaFact]
    public void AScreenThatIsNoLongerThereIsNotInsistedOn()
    {
        var (_, viewState, _) = TestSurface.Create();

        viewState.Save(new ViewState
        {
            MainWindow = new WindowPlacement
            {
                Width = 500,
                Height = 400,
                Maximized = true,
                MaximizedOnX = -30_000,
                MaximizedOnY = -30_000,
            },
        });

        var window = new Window { Width = 900, Height = 700 };
        WindowPlacementMemory.Attach(window, viewState);

        Assert.NotEqual(new PixelPoint(-30_000, -30_000), window.Position);
        Assert.Equal(WindowState.Maximized, window.WindowState);
    }


    /// <summary>
    /// Bigger text wants a bigger window to put it in, so the opening size follows the zoom.
    /// A size the Commander chose does not: that one is not a guess to be improved on.
    /// <para>
    /// The base it scales is a proportion of the screen, not a number in the XAML — which is
    /// why this reads the proportion rather than a literal. A fixed default is a default
    /// chosen against somebody else's monitor.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheDefaultSizeFollowsTheZoomButARememberedOneDoesNot()
    {
        var (_, viewState, _) = TestSurface.Create();

        var fresh = new Window { Width = 800, Height = 600 };
        WindowPlacementMemory.Attach(fresh, viewState, () => 125);

        var screen = fresh.Screens!.Primary ?? fresh.Screens.All[0];
        var (openWidth, openHeight) = WindowFit.Opening(
            screen.WorkingArea.Width / screen.Scaling,
            screen.WorkingArea.Height / screen.Scaling);

        var (expectedWidth, expectedHeight) = WindowFit.Clamp(
            openWidth * 1.25,
            openHeight * 1.25,
            screen.WorkingArea.Width / screen.Scaling,
            screen.WorkingArea.Height / screen.Scaling);

        Assert.Equal(expectedWidth, fresh.Width, 3);
        Assert.Equal(expectedHeight, fresh.Height, 3);

        // And it is genuinely larger than the unzoomed opening size, or the assertion above
        // would pass on a clamp that had swallowed the zoom entirely.
        Assert.True(fresh.Width > openWidth);

        viewState.Save(viewState.Load() with
        {
            MainWindow = new WindowPlacement { Width = 900, Height = 700 },
        });

        var chosen = new Window { Width = 800, Height = 600 };
        WindowPlacementMemory.Attach(chosen, viewState, () => 125);

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
