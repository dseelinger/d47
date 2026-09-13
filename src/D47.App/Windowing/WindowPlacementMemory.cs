using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Windowing;

/// <summary>
/// Opens the window at a size that fits, and remembers where the Commander left it (Phase 9, "Open at a
/// size that fits the screen").
/// </summary>
public sealed class WindowPlacementMemory
{
    private readonly Window _window;
    private readonly ViewStateStore _store;

    private WindowPlacement _last;

    private WindowPlacementMemory(Window window, ViewStateStore store, WindowPlacement seed)
    {
        _window = window;
        _store = store;
        _last = seed;
    }

    /// <summary>
    /// Applies the remembered placement, or clamps the default to the screen, then keeps watching.
    /// </summary>
    /// <param name="zoom">The zoom level the surface will be drawn at, when it has one.</param>
    public static WindowPlacementMemory Attach(Window window, ViewStateStore store, Func<int>? zoom = null)
    {
        var state = store.Load();
        var remembered = state.MainWindow;

        var scale = remembered is null && zoom is not null ? ZoomLadder.ScaleOf(zoom()) : 1.0;

        var screen = ScreenFor(window, remembered);

        // The opening size is a proportion of the screen in front of the Commander, not a number in the XAML.
        var (openWidth, openHeight) = screen is not null
            ? WindowFit.Opening(
                screen.WorkingArea.Width / screen.Scaling,
                screen.WorkingArea.Height / screen.Scaling)
            : (window.Width, window.Height);

        var width = remembered?.Width > 0 ? remembered.Width : openWidth * scale;
        var height = remembered?.Height > 0 ? remembered.Height : openHeight * scale;

        if (screen is not null)
        {
            // The work area is physical pixels and the window is sized in device-independent ones.
            (width, height) = WindowFit.Clamp(
                width,
                height,
                screen.WorkingArea.Width / screen.Scaling,
                screen.WorkingArea.Height / screen.Scaling);
        }

        window.Width = width;
        window.Height = height;

        if (remembered is { X: { } x, Y: { } y } && screen is not null)
        {
            var physical = new FitRect(x, y, width * screen.Scaling, height * screen.Scaling);
            var area = new FitRect(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height);

            if (WindowFit.Reposition(physical, [area]) is not null)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint((int)x, (int)y);
            }
        }

        if (remembered?.Maximized == true)
        {
            // Onto the screen it was maximised on, before it is maximised.
            if (remembered is { MaximizedOnX: { } screenX, MaximizedOnY: { } screenY }
                && window.Screens?.ScreenFromPoint(new PixelPoint((int)screenX, (int)screenY)) is not null)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint((int)screenX, (int)screenY);
            }

            window.WindowState = WindowState.Maximized;
        }

        var seed = new WindowPlacement
        {
            Width = width,
            Height = height,
            X = remembered?.X,
            Y = remembered?.Y,
            Maximized = remembered?.Maximized ?? false,
            MaximizedOnX = remembered?.MaximizedOnX,
            MaximizedOnY = remembered?.MaximizedOnY,
        };

        var memory = new WindowPlacementMemory(window, store, seed);

        // Sampled while the window is in its normal state rather than read at close, because a maximised
        // window reports the maximised rectangle and restoring to that would leave a Commander who maximises
        // once with a window that can never be un-maximised back to a size they chose.
        window.Resized += (_, _) => memory.SampleWhenSettled();
        window.PositionChanged += (_, _) => memory.SampleWhenSettled();

        window.Closing += (_, _) =>
        {
            // Directly, not posted: there may be no dispatcher pass left to run a deferred one in.
            memory.Sample();
            memory.Save();
        };

        return memory;
    }

    /// <summary>Whether a deferred sample is already on its way, so a burst costs one.</summary>
    private bool _sampling;

    private void SampleWhenSettled()
    {
        if (_sampling)
        {
            return;
        }

        _sampling = true;

        Dispatcher.UIThread.Post(
            () =>
            {
                _sampling = false;
                Sample();
            },
            DispatcherPriority.Background);
    }

    private static Screen? ScreenFor(Window window, WindowPlacement? remembered)
    {
        var screens = window.Screens;

        if (screens is null)
        {
            return null;
        }

        if (remembered is { X: { } x, Y: { } y }
            && screens.ScreenFromPoint(new PixelPoint((int)x, (int)y)) is { } saved)
        {
            return saved;
        }

        // No remembered position means the platform is about to centre it, which puts it on whichever screen
        // the platform calls primary.
        return screens.Primary ?? screens.All.FirstOrDefault();
    }

    private void Sample()
    {
        if (_window.WindowState != WindowState.Normal)
        {
            var maximised = _window.WindowState == WindowState.Maximized;

            // Which screen, while there is still a window on one to ask about.
            var screen = maximised ? _window.Screens?.ScreenFromWindow(_window) : null;

            _last = _last with
            {
                Maximized = maximised,
                MaximizedOnX = screen?.WorkingArea.X ?? _last.MaximizedOnX,
                MaximizedOnY = screen?.WorkingArea.Y ?? _last.MaximizedOnY,
            };

            return;
        }

        var previous = _last;

        _last = new WindowPlacement
        {
            Width = _window.Width,
            Height = _window.Height,
            X = _window.Position.X,
            Y = _window.Position.Y,
            Maximized = false,

            // Kept rather than cleared.
            MaximizedOnX = previous.MaximizedOnX,
            MaximizedOnY = previous.MaximizedOnY,
        };
    }

    private void Save()
    {
        if (_last.Width <= 0 || _last.Height <= 0)
        {
            return;
        }

        // Read-modify-write against the file rather than against a snapshot taken at startup: the settings
        // page writes card collapse state into the same store while this window is open, and saving a stale
        // copy here would silently undo it.
        var state = _store.Load();
        _store.Save(state.With(_last));
    }
}
