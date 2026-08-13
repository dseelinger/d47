using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
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
    private readonly WindowSlot _slot;

    private WindowPlacement _last;

    private WindowPlacementMemory(
        Window window,
        ViewStateStore store,
        WindowSlot slot,
        WindowPlacement seed)
    {
        _window = window;
        _store = store;
        _slot = slot;
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
        WindowSlot slot = WindowSlot.Main,
        Func<int>? zoom = null)
    {
        var state = store.Load();
        var remembered = state.PlacementOf(slot);

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
            window.WindowState = WindowState.Maximized;
        }

        var memory = new WindowPlacementMemory(
            window,
            store,
            slot,
            new WindowPlacement
            {
                Width = width,
                Height = height,
                X = remembered?.X,
                Y = remembered?.Y,
                Maximized = remembered?.Maximized ?? false,
            });

        // Sampled while the window is in its normal state rather than read at close, because a
        // maximised window reports the maximised rectangle and restoring to that would leave a
        // Commander who maximises once with a window that can never be un-maximised back to a
        // size they chose.
        window.Resized += (_, _) => memory.Sample();
        window.PositionChanged += (_, _) => memory.Sample();
        window.Closing += (_, _) => memory.Save();

        return memory;
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
            _last = _last with { Maximized = _window.WindowState == WindowState.Maximized };
            return;
        }

        _last = new WindowPlacement
        {
            Width = _window.Width,
            Height = _window.Height,
            X = _window.Position.X,
            Y = _window.Position.Y,
            Maximized = false,
        };
    }

    private void Save()
    {
        if (_last.Width <= 0 || _last.Height <= 0)
        {
            return;
        }

        // Read-modify-write against the file rather than against a snapshot taken at startup:
        // the settings window writes card collapse state into the same store while this window
        // is open, and saving a stale copy here would quietly undo it.
        _store.Save(_store.Load().With(_slot, _last));
    }
}
