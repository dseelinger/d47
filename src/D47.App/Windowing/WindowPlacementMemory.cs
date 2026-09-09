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

    /// <summary>The mini rectangle, kept apart from the full one (Phase 51).</summary>
    private WindowPlacement? _lastMini;

    /// <summary>Which of the two shapes the window is in, so a sample lands in the right one.</summary>
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
    /// Applies the remembered placement, or clamps the default to the screen, then keeps watching.
    /// </summary>
    /// <param name="zoom">The zoom level the surface will be drawn at, when it has one.</param>
    /// <param name="startMini">Whether the window is opening in mini (Phase 51).</param>
    public static WindowPlacementMemory Attach(
        Window window,
        ViewStateStore store,
        Func<int>? zoom = null,
        bool startMini = false,
        Size? miniSize = null)
    {
        var state = store.Load();

        // The rectangle for the shape it is opening in.
        var remembered = (startMini ? state.MainWindowMini : state.MainWindow) ?? state.MainWindow;

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

        var memory = new WindowPlacementMemory(
            window,
            store,
            startMini ? state.MainWindow ?? seed : seed,
            startMini ? seed : state.MainWindowMini,
            startMini);

        // A window opening in mini for the first time has no rectangle of its own, so it has just been sized
        // as the full window.
        if (startMini && state.MainWindowMini is null && miniSize is { } wanted)
        {
            window.Width = wanted.Width;
            window.Height = wanted.Height;

            memory._lastMini = seed with { Width = wanted.Width, Height = wanted.Height };
        }

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

            // Which screen, while there is still a window on one to ask about.
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

            // Kept rather than cleared.
            MaximizedOnX = previous.MaximizedOnX,
            MaximizedOnY = previous.MaximizedOnY,
        });
    }

    /// <summary>
    /// Puts the window into its other shape (Phase 51): the rectangle it is leaving is written down,
    /// and the one it is going to is applied.
    /// </summary>
    /// <param name="mini">Which shape it is going into.</param>
    /// <param name="measured">
    /// What mini wants, for the first time there is no mini rectangle to go back to.
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

        // A maximised window that goes mini has to come out of it first, or the platform ignores the size and
        // the Commander gets a full-screen window with a strip of content in it.
        if (mini && _window.WindowState != WindowState.Normal)
        {
            _window.WindowState = WindowState.Normal;
        }

        var wanted = mini ? _lastMini : _last;

        if (mini && wanted is null && measured is { } size)
        {
            // The first mini takes the full window's corner, so the strip appears where the window already
            // was rather than jumping across the desk on its first use.
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
    /// The measured size mini wants, applied to a window already in mini — what a zoom change means
    /// (Phase 51).
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
        // Both, because a mini toggle writes one of them and the Commander may never open the other again
        // before closing.
        var state = _store.Load();

        if (_last.Width > 0 && _last.Height > 0)
        {
            state = state.With(_last, mini: false);
        }

        if (_lastMini is { Width: > 0, Height: > 0 } mini)
        {
            state = state.With(mini, mini: true);
        }

        // Read-modify-write against the file rather than against a snapshot taken at startup: the settings
        // page writes card collapse state into the same store while this window is open, and saving a stale
        // copy here would quietly undo it.
        _store.Save(state);
    }
}
