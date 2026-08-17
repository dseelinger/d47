using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Windowing;

/// <summary>
/// Opens the window at a size that fits, and remembers where the Commander left it
/// (list.md Phase 9, "Open at a size that fits the screen").
/// <para>
/// The two halves are one feature. Clamping alone would re-derive a size every launch and
/// throw away whatever the Commander resized it to; remembering alone would never help the
/// first launch, which is the only one the default has to survive. Together the default only
/// has to be right once.
/// </para>
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
    /// Applies the remembered placement, or clamps the default to the screen, then keeps
    /// watching. Call before the window is shown: setting the size afterwards is a visible
    /// resize, which reads as the app changing its mind.
    /// </summary>
    /// <param name="zoom">
    /// The zoom level the surface will be drawn at, when it has one. Zoom scrolls the window
    /// horizontally rather than reflowing the layout (list.md Phase 9), so at 125% the content
    /// is a quarter wider than at 100% and a default chosen at 100% clips on every launch.
    /// Only the unremembered default scales: a size the Commander chose is a size they chose.
    /// </param>
    public static WindowPlacementMemory Attach(
        Window window,
        ViewStateStore store,
        Func<int>? zoom = null)
    {
        var state = store.Load();
        var remembered = state.MainWindow;

        var scale = remembered is null && zoom is not null ? ZoomLadder.ScaleOf(zoom()) : 1.0;

        var screen = ScreenFor(window, remembered);

        // The opening size is a proportion of the screen in front of the Commander, not a
        // number in the XAML. The XAML's own Width and Height stay as the answer for a machine
        // that will not say how big its screens are, which is the only case left.
        var (openWidth, openHeight) = screen is not null
            ? WindowFit.Opening(
                screen.WorkingArea.Width / screen.Scaling,
                screen.WorkingArea.Height / screen.Scaling)
            : (window.Width, window.Height);

        var width = remembered?.Width > 0 ? remembered.Width : openWidth * scale;
        var height = remembered?.Height > 0 ? remembered.Height : openHeight * scale;

        if (screen is not null)
        {
            // The work area is physical pixels and the window is sized in device-independent
            // ones. Converting here, once, is what makes the clamp mean what it says: the whole
            // bug is a default chosen in one unit and checked against the other.
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
            // Onto the screen it was maximised on, before it is maximised. Windows maximises a
            // window to whichever monitor it is on, so putting it there first is the whole of
            // "on the same monitor, if possible" — and the "if possible" is the screen lookup:
            // a monitor that has been unplugged since falls through and the platform decides.
            if (remembered is { MaximizedOnX: { } screenX, MaximizedOnY: { } screenY }
                && window.Screens?.ScreenFromPoint(new PixelPoint((int)screenX, (int)screenY)) is not null)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint((int)screenX, (int)screenY);
            }

            window.WindowState = WindowState.Maximized;
        }

        var memory = new WindowPlacementMemory(
            window,
            store,
            new WindowPlacement
            {
                Width = width,
                Height = height,
                X = remembered?.X,
                Y = remembered?.Y,
                Maximized = remembered?.Maximized ?? false,
                MaximizedOnX = remembered?.MaximizedOnX,
                MaximizedOnY = remembered?.MaximizedOnY,
            });

        // Sampled while the window is in its normal state rather than read at close, because a
        // maximised window reports the maximised rectangle and restoring to that would leave a
        // Commander who maximises once with a window that can never be un-maximised back to a
        // size they chose.
        //
        // Deferred rather than read inside the handler, which is a guard and not a diagnosis.
        // A window manager is free to raise the resize and the move for a maximise before the
        // WindowState property has caught up, and a sample taken mid-burst would then read
        // Normal, write the maximised rectangle down as a size the Commander had chosen, and
        // record Maximized as false. Posting lets the burst finish, so the sample reads the
        // state the window settled in whatever order the platform raised things in.
        //
        // <b>Unproven against Win32.</b> Avalonia's headless platform raises these in the
        // settled order, so a test cannot tell this apart from the synchronous version; the
        // reason to prefer it is that it does not depend on the order at all.
        window.Resized += (_, _) => memory.SampleWhenSettled();
        window.PositionChanged += (_, _) => memory.SampleWhenSettled();

        window.Closing += (_, _) =>
        {
            // Directly, not posted: there may be no dispatcher pass left to run a deferred one
            // in. By now the state is settled anyway, which is the only thing the deferral buys.
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

        // No remembered position means the platform is about to centre it, which puts it on
        // whichever screen the platform calls primary.
        return screens.Primary ?? screens.All.FirstOrDefault();
    }

    private void Sample()
    {
        if (_window.WindowState != WindowState.Normal)
        {
            var maximised = _window.WindowState == WindowState.Maximized;

            // Which screen, while there is still a window on one to ask about. Minimised is the
            // other branch through here and answers nothing: a minimised window is on no screen,
            // and the answer from before it was minimised is the one worth keeping.
            var screen = maximised ? _window.Screens?.ScreenFromWindow(_window) : null;

            _last = _last with
            {
                Maximized = maximised,
                MaximizedOnX = screen?.WorkingArea.X ?? _last.MaximizedOnX,
                MaximizedOnY = screen?.WorkingArea.Y ?? _last.MaximizedOnY,
            };

            return;
        }

        _last = new WindowPlacement
        {
            Width = _window.Width,
            Height = _window.Height,
            X = _window.Position.X,
            Y = _window.Position.Y,
            Maximized = false,

            // Kept rather than cleared. Un-maximising does not un-choose the monitor, and a
            // Commander who maximises again before closing should not have to re-teach it.
            MaximizedOnX = _last.MaximizedOnX,
            MaximizedOnY = _last.MaximizedOnY,
        };
    }

    private void Save()
    {
        if (_last.Width <= 0 || _last.Height <= 0)
        {
            return;
        }

        // Read-modify-write against the file rather than against a snapshot taken at startup:
        // the settings page writes card collapse state into the same store while this window is
        // open, and saving a stale copy here would quietly undo it.
        _store.Save(_store.Load().With(_last));
    }
}
