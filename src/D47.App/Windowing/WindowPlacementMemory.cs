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

    /// <summary>
    /// The mini rectangle, kept apart from the full one (list.md Phase 51). Null until the
    /// Commander has been there.
    /// </summary>
    private WindowPlacement? _lastMini;

    /// <summary>
    /// Which of the two shapes the window is in, so a sample lands in the right one.
    /// <para>
    /// <b>This field is the whole of the trap.</b> Without it a mini toggle is just a resize, and
    /// this record writes every resize down as a size the Commander chose — so shrinking once
    /// would overwrite the full-window rectangle and the way back would arrive 512 pixels wide,
    /// permanently and across a restart.
    /// </para>
    /// </summary>
    private bool _mini;

    private WindowPlacementMemory(
        Window window, ViewStateStore store, WindowPlacement seed, WindowPlacement? mini, bool startMini)
    {
        _window = window;
        _store = store;
        _last = seed;
        _lastMini = mini;
        _mini = startMini;
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
    /// <param name="startMini">
    /// Whether the window is opening in mini (list.md Phase 51). It opens on the mini rectangle
    /// when it has one, and the caller measures one for it when it does not — see
    /// <see cref="Resize"/>, which is what the window calls on every toggle after this.
    /// </param>
    public static WindowPlacementMemory Attach(
        Window window,
        ViewStateStore store,
        Func<int>? zoom = null,
        bool startMini = false,
        Size? miniSize = null)
    {
        var state = store.Load();

        // The rectangle for the shape it is opening in. A mini window with no rectangle yet falls
        // through to the full one and is resized by the caller the moment it has measured one,
        // which is the same path a toggle takes.
        var remembered = (startMini ? state.MainWindowMini : state.MainWindow) ?? state.MainWindow;

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

        var memory = new WindowPlacementMemory(
            window,
            store,
            startMini ? state.MainWindow ?? seed : seed,
            startMini ? seed : state.MainWindowMini,
            startMini);

        // A window opening in mini for the first time has no rectangle of its own, so it has just
        // been sized as the full window. Sized here rather than after it is shown, because the
        // alternative is a Commander watching it shrink.
        if (startMini && state.MainWindowMini is null && miniSize is { } wanted)
        {
            window.Width = wanted.Width;
            window.Height = wanted.Height;

            memory._lastMini = seed with { Width = wanted.Width, Height = wanted.Height };
        }

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

    /// <summary>The rectangle for the shape the window is in now.</summary>
    private WindowPlacement Current => _mini ? _lastMini ?? _last : _last;

    private void Record(WindowPlacement placement)
    {
        if (_mini)
        {
            _lastMini = placement;
        }
        else
        {
            _last = placement;
        }
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
            var was = Current;

            Record(was with
            {
                Maximized = maximised,
                MaximizedOnX = screen?.WorkingArea.X ?? was.MaximizedOnX,
                MaximizedOnY = screen?.WorkingArea.Y ?? was.MaximizedOnY,
            });

            return;
        }

        var previous = Current;

        Record(new WindowPlacement
        {
            Width = _window.Width,
            Height = _window.Height,
            X = _window.Position.X,
            Y = _window.Position.Y,
            Maximized = false,

            // Kept rather than cleared. Un-maximising does not un-choose the monitor, and a
            // Commander who maximises again before closing should not have to re-teach it.
            MaximizedOnX = previous.MaximizedOnX,
            MaximizedOnY = previous.MaximizedOnY,
        });
    }

    /// <summary>
    /// Puts the window into its other shape (list.md Phase 51): the rectangle it is leaving is
    /// written down, and the one it is going to is applied.
    /// <para>
    /// <b>Called before the content changes, not after.</b> The sample that has to happen is a
    /// sample of the shape being left, and a resize raised by the new content would beat it.
    /// </para>
    /// </summary>
    /// <param name="mini">Which shape it is going into.</param>
    /// <param name="measured">
    /// What mini wants, for the first time there is no mini rectangle to go back to. Measured by
    /// the caller because only the caller can see the content — see <c>MainWindow.MiniSize</c>.
    /// Ignored on the way back to full, which always has a rectangle.
    /// </param>
    public void Resize(bool mini, Size? measured)
    {
        if (mini == _mini)
        {
            return;
        }

        // The shape being left, before anything moves.
        Sample();
        Save();

        _mini = mini;

        // A maximised window that goes mini has to come out of it first, or the platform ignores
        // the size and the Commander gets a full-screen window with a strip of content in it.
        if (mini && _window.WindowState != WindowState.Normal)
        {
            _window.WindowState = WindowState.Normal;
        }

        var wanted = mini ? _lastMini : _last;

        if (mini && wanted is null && measured is { } size)
        {
            // The first mini takes the full window's corner, so the strip appears where the window
            // already was rather than jumping across the desk on its first use.
            wanted = new WindowPlacement
            {
                Width = size.Width,
                Height = size.Height,
                X = _window.Position.X,
                Y = _window.Position.Y,
            };
        }

        if (wanted is null)
        {
            return;
        }

        Apply(wanted);
        Record(wanted);
    }

    /// <summary>
    /// The measured size mini wants, applied to a window already in mini — what a zoom change
    /// means (list.md Phase 51).
    /// <para>
    /// The layout transform re-measures, so a mini window at 150% is a bigger mini window rather
    /// than a clipped one; this is the half of that the window has to do for itself, because
    /// nothing else resizes it.
    /// </para>
    /// </summary>
    public void Remeasured(Size size)
    {
        if (!_mini || _window.WindowState != WindowState.Normal)
        {
            return;
        }

        _window.Width = size.Width;
        _window.Height = size.Height;
    }

    private void Apply(WindowPlacement placement)
    {
        if (placement.Width > 0 && placement.Height > 0)
        {
            _window.Width = placement.Width;
            _window.Height = placement.Height;
        }

        if (placement is { X: { } x, Y: { } y })
        {
            _window.Position = new PixelPoint((int)x, (int)y);
        }

        if (placement.Maximized)
        {
            _window.WindowState = WindowState.Maximized;
        }
    }

    private void Save()
    {
        // Both, because a mini toggle writes one of them and the Commander may never open the
        // other again before closing. Each is skipped rather than the pair abandoned when it holds
        // nothing worth keeping — a mini rectangle that was never reached is null, not zero.
        var state = _store.Load();

        if (_last.Width > 0 && _last.Height > 0)
        {
            state = state.With(_last, mini: false);
        }

        if (_lastMini is { Width: > 0, Height: > 0 } mini)
        {
            state = state.With(mini, mini: true);
        }

        // Read-modify-write against the file rather than against a snapshot taken at startup:
        // the settings page writes card collapse state into the same store while this window is
        // open, and saving a stale copy here would quietly undo it.
        _store.Save(state);
    }
}
