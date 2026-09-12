using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using D47.App.Input;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging;

namespace D47.App.Windowing;

/// <summary>
/// The mini panel without a headset: a chromeless, click-through strip pinned over the game (Phase 48).
/// </summary>
public sealed class OverlayPanel : Window
{
    private const int GwlExStyle = -20;

    /// <summary>The pointer passes through: a click on the strip is a click Elite receives.</summary>
    private const int WsExTransparent = 0x00000020;

    /// <summary>It never takes the foreground, so nothing it does can steal focus from the game.</summary>
    private const int WsExNoActivate = 0x08000000;

    /// <summary>It is not something to Alt-Tab into, because it is not somewhere to go.</summary>
    private const int WsExToolWindow = 0x00000080;

    /// <summary>Layered, so the opacity row can be an alpha against the cockpit behind it.</summary>
    private const int WsExLayered = 0x00080000;

    private const int LwaAlpha = 0x00000002;

    private readonly PanelView _view;
    private readonly ScaleTransform _scale = new();
    private readonly Border _frame;
    private readonly SettingsService _settings;
    private readonly ViewStateStore _viewState;
    private readonly ILogger _logger;

    /// <summary>Whether Elite has the foreground, as this surface asks it.</summary>
    private Func<bool> _eliteInFront = () => false;

    /// <summary>
    /// Elite's window, for the one question this surface asks of it beyond the foreground: which
    /// monitor it is on (#36).
    /// </summary>
    private IEliteWindow? _elite;

    /// <summary>Whether the Commander has put the strip somewhere themselves (#36).</summary>
    private bool _placed;

    private bool _placing;
    private Point? _grab;

    /// <summary>
    /// Whether this window has been closed — which nothing in d47 does, and which happened once anyway
    /// (#285).
    /// </summary>
    private bool _closed;

    /// <summary>
    /// Whether d47 is on its way out, so that the close every clean quit performs is not reported as
    /// the one nothing explains.
    /// </summary>
    private bool _quitting;

    private int _appliedScale = ZoomLadder.Default;

    /// <summary>
    /// <param name="tabs"> The pages this strip carries beyond the transcript (asked for 2026-08-24:
    /// "it should have the same tabs as the VR mini panel, including Checklist").
    /// </summary>
    /// <param name="tabs">
    /// The pages this strip carries beyond the transcript (asked for 2026-08-24: "it should have the
    /// same tabs as the VR mini panel, including Checklist").
    /// </param>
    public OverlayPanel(
        PanelViewModel model,
        SettingsService settings,
        ViewStateStore viewState,
        ILogger logger,
        AvatarLibrary? avatars = null,
        AdventureSurface? adventures = null,
        OverlayTabs? tabs = null)
    {
        _settings = settings;
        _viewState = viewState;
        _logger = logger;

        _view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        _view.Avatar.Library = avatars;

        // No buttons on a surface nothing can press (asked for 2026-08-24).
        _view.Classes.Add("output-only");

        if (adventures is not null)
        {
            // The story, at mini's size (Phase 48). These two roots are all the overlay furnishes —
            // the transcript, which every surface has by construction, and this.
            _view.EnableAdventures(adventures);
        }

        Furnish(tabs);

        // The same scaling host the window zooms with, for the same reason: a render transform would draw the
        // strip larger and let it clip, where a layout transform re-measures so text rewraps and spacing
        // grows with it.
        _frame = new Border
        {
            BorderThickness = new Thickness(0),
            Child = new LayoutTransformControl { LayoutTransform = _scale, Child = _view },
        };

        Content = _frame;

        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        SizeToContent = SizeToContent.Manual;
        Title = "Directive 47 overlay";

        // The panel paints its own background over every pixel of this window, so what this brush is is only
        // ever seen for the width of a layout pass.
        this.Bind(BackgroundProperty, this.GetResourceObservable(Theming.ThemeManager.BackgroundKey));

        ApplyScale();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    /// <summary>The rest of the headset's pages, where the app has them to give (asked for 2026-08-24).</summary>
    private void Furnish(OverlayTabs? tabs)
    {
        if (tabs is null)
        {
            return;
        }

        if (tabs.Checklists is { } checklists)
        {
            // What the Commander is working on — the tab this instruction named.
            _view.EnableChecklist(checklists, tabs.Goals, tabs.BackfillGoals);
        }

        if (tabs.Unlocks is { } unlocks && tabs.Ships is { } ships && tabs.GameState is { } state)
        {
            _view.EnableEngineers(unlocks, ships, state, tabs.OnFoot, tabs.EngineersMemory);
        }

        if (tabs.Timekeeper is { } timekeeper && tabs.Alarms is { } alarms)
        {
            _view.EnableUtilities(
                timekeeper, alarms, () => D47.Core.SystemWallClock.Instance.UtcNow, () => TimeZoneInfo.Local);
        }
    }

    /// <summary>
    /// Redraws the pages that change with nothing having happened — the clocks, and the engineer
    /// ranking when the Commander has moved or re-fitted (Phases 24 and 28).
    /// </summary>
    private void TickPages()
    {
        _view.TickClocks();
        _view.TickEngineers();
    }

    /// <summary>
    /// Where this surface is, so a spoken phrase and a switch can move it (Phase 45 and Phase 46).
    /// </summary>
    public PanelNavigator Nav => _view.Nav;

    /// <summary>Moves the page the strip is showing, for a spoken scroll (#34).</summary>
    public PanelScrollOutcome Scroll(PanelScrollStep step) =>
        // A strip nobody can see has nothing to scroll, which is a different answer from being at the end of
        // something (#263) — and the one that lets a hidden surface stay silent while the window it is beside
        // answers for the phrase.
        IsVisible ? _view.Scroll(step) : PanelScrollOutcome.NothingToScroll;

    /// <summary>Whether the strip is in place mode — taking clicks so it can be dragged.</summary>
    public bool IsPlacing => _placing;

    /// <summary>Whether the strip should be on screen at all.</summary>
    public static bool ShouldShow(bool enabled, bool eliteForeground, bool placing) =>
        enabled && (placing || eliteForeground);

    /// <summary>
    /// Builds the overlay, applies the remembered placement and subscribes it to the tick and to
    /// settings.
    /// </summary>
    public static OverlayPanel Attach(
        PanelViewModel model,
        SettingsService settings,
        ViewStateStore viewState,
        TickLoop tick,
        IEliteWindow elite,
        ILogger logger,
        AvatarLibrary? avatars = null,
        AdventureSurface? adventures = null,
        OverlayTabs? tabs = null)
    {
        var overlay = new OverlayPanel(model, settings, viewState, logger, avatars, adventures, tabs)
        {
            _eliteInFront = () => elite.IsForeground,
            _elite = elite,
        };

        overlay.Restore();
        overlay.ApplyVisibility();

        // Captured on this surface's own thread rather than read from the static one at call time.
        var ui = Dispatcher.UIThread;

        // Quitting closes every window there is, and that close is the one thing OnClosed must not report as
        // unexplained (#285).
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) => overlay._quitting = true;
        }

        settings.Changed += change => ui.Post(() => overlay.Configure(change.Key));

        // Polled on the tick, like everything else that reads the world.
        tick.Add("overlay", _ => ui.Post(overlay.ApplyVisibility));

        // And the pages that move with nothing having happened.
        tick.Add("overlay-pages", _ => ui.Post(() =>
        {
            if (overlay.IsVisible)
            {
                overlay.TickPages();
            }
        }));

        return overlay;
    }

    /// <summary>
    /// Hands the pointer back to the strip so it can be dragged, and takes it away again the moment the
    /// Commander lets go (Phase 48).
    /// </summary>
    public void Place()
    {
        if (_closed)
        {
            _logger.LogWarning("The overlay strip has been closed, so there is nothing to place; restart d47 to bring it back");
            return;
        }

        if (_placing)
        {
            // A second press with nothing dragged is the Commander changing their mind, which is the only
            // reading of it that means anything.
            Settle();
            return;
        }

        _placing = true;

        // Shown before the styles are re-applied, because there is no window handle to set an extended style
        // on until there is a window.
        ApplyVisibility();
        ApplyStyles();

        _frame.BorderThickness = new Thickness(2);
        _frame.Bind(Border.BorderBrushProperty, this.GetResourceObservable(Theming.ThemeManager.AccentKey));

        _logger.LogInformation("The overlay is in place mode; drag it and let go");
    }

    /// <summary>Re-reads whatever the changed row means for this surface.</summary>
    private void Configure(string key)
    {
        if (key == InterfaceCapability.OverlayScaleKey)
        {
            ApplyScale();
        }

        if (key == InterfaceCapability.OverlayOpacityKey)
        {
            ApplyOpacity();
        }

        if (key == InterfaceCapability.OverlayKey
            || key == InterfaceCapability.OverlayScaleKey)
        {
            ApplyVisibility();
        }
    }

    /// <summary>Opens or closes the strip.</summary>
    private void ApplyVisibility()
    {
        if (_closed)
        {
            // A closed strip stays down for the rest of the run (<a
            // href=".com/dseelinger/d47/issues/285">#285</a>).
            return;
        }

        var wanted = ShouldShow(_settings.Current.Ui.Overlay.Enabled, _eliteInFront(), _placing);

        if (wanted == IsVisible)
        {
            return;
        }

        if (!wanted)
        {
            if (_placing)
            {
                Settle();
            }

            Hide();
            return;
        }

        // Which monitor, asked now rather than at startup (#36).
        Park();

        Show();

        // After the window exists, which is the first moment there is a handle to style.
        ApplyStyles();
        ApplyOpacity();
    }

    /// <summary>
    /// Records that the strip is gone, and — unless d47 is quitting — says what closed it (#285).
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _closed = true;

        // Not Settle(), which would write the position of a window that no longer has one.
        _placing = false;
        _grab = null;

        if (_quitting)
        {
            return;
        }

        _logger.LogWarning(
            "Something closed the overlay strip; it stays down until d47 is restarted. Nothing in d47 closes it, so this is where it went: {Stack}",
            Environment.StackTrace);
    }

    /// <summary>The three extended styles, each of them its own claim about what this surface is.</summary>
    private void ApplyStyles()
    {
        if (!OperatingSystem.IsWindows() || Handle() is not { } handle)
        {
            return;
        }

        try
        {
            var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();

            // Layered too, because that is what lets the opacity row mean anything: an alpha set with
            // SetLayeredWindowAttributes is applied against what is behind the window, which is the game.
            styles |= WsExNoActivate | WsExToolWindow | WsExLayered;

            if (_placing)
            {
                styles &= ~WsExTransparent;
            }
            else
            {
                styles |= WsExTransparent;
            }

            SetWindowLongPtr(handle, GwlExStyle, new IntPtr(styles));
        }
        catch (EntryPointNotFoundException ex)
        {
            // The 32-bit entry point is SetWindowLongW and d47 is x64, so this is unreachable in any shipped
            // build - but a swallowed style is a strip that absorbs clicks, which is the one failure this
            // surface must not have without saying so.
            _logger.LogWarning(ex, "Could not make the overlay click-through; it is being hidden instead");
            Hide();
        }
    }

    private void ApplyScale()
    {
        var percent = ZoomLadder.Snap(_settings.Current.Ui.Overlay.ScalePercent);

        if (percent == _appliedScale && Width > 0)
        {
            return;
        }

        _appliedScale = percent;

        var factor = ZoomLadder.ScaleOf(percent);

        _scale.ScaleX = factor;
        _scale.ScaleY = factor;

        // The headset's mini panel is fixed at 512x280 because apparent size there is the pixel count and the
        // quad's width in metres together.
        Width = PanelResolution.Mini.Width * factor;
        Height = PanelResolution.Mini.Height * factor;

        // The remembered corner is the top-left, so a strip that grew still starts where it was put and is
        // pushed back on screen only if growing took it off.
        Clamp();
    }

    /// <summary>How much cockpit shows through, as one alpha over the whole strip.</summary>
    private void ApplyOpacity()
    {
        var solid = Math.Clamp(_settings.Current.Ui.Overlay.Opacity, 0.2, 1);

        if (!OperatingSystem.IsWindows() || Handle() is not { } handle)
        {
            return;
        }

        SetLayeredWindowAttributes(handle, 0, (byte)Math.Round(solid * 255), LwaAlpha);
    }

    /// <summary>Where the Commander left it, if they ever said.</summary>
    private void Restore()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (_viewState.Load().Overlay is not { } placement)
        {
            Park();
            return;
        }

        _placed = true;
        Position = new PixelPoint((int)placement.X, (int)placement.Y);
        Clamp();
    }

    /// <summary>Puts the strip in the bottom-right of the screen Elite is on (#36).</summary>
    private void Park()
    {
        if (_placed || ScreenForElite() is not { } screen)
        {
            return;
        }

        const int Margin = 24;

        var area = screen.WorkingArea;

        Position = new PixelPoint(
            area.X + area.Width - (int)(Width * screen.Scaling) - Margin,
            area.Y + area.Height - (int)(Height * screen.Scaling) - Margin);
    }

    /// <summary>
    /// The screen Elite's window is on, or the primary one when d47 cannot tell — which is every case
    /// where the game is not running, and is why this feature can be set up before it is.
    /// </summary>
    private Screen? ScreenForElite()
    {
        if (Screens is not { } screens || screens.All.Count == 0)
        {
            return null;
        }

        var fallback = screens.Primary ?? screens.All[0];

        if (_elite?.Bounds is not { } elite || elite.Width <= 0 || elite.Height <= 0)
        {
            return fallback;
        }

        var centre = new PixelPoint(elite.X + (elite.Width / 2), elite.Y + (elite.Height / 2));

        return screens.ScreenFromPoint(centre) ?? fallback;
    }

    /// <summary>Pushes the strip back onto a screen it is at least partly on.</summary>
    private void Clamp()
    {
        if (Screens is not { } screens || screens.All.Count == 0)
        {
            return;
        }

        if (screens.ScreenFromPoint(Position) is not null)
        {
            return;
        }

        // Onto the screen the game is on, which is the one the Commander is looking at — the same answer Park
        // gives, rather than the primary monitor this used to fall back to (#36).
        var area = (ScreenForElite() ?? screens.Primary ?? screens.All[0]).WorkingArea;

        Position = new PixelPoint(area.X + area.Width / 2, area.Y + area.Height / 2);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_placing)
        {
            return;
        }

        _grab = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    /// <summary>Moves the window by however far the pointer has travelled since it was grabbed.</summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_placing || _grab is not { } grab)
        {
            return;
        }

        var now = e.GetPosition(this);
        var scaling = Screens?.ScreenFromWindow(this)?.Scaling ?? 1.0;

        Position = new PixelPoint(
            Position.X + (int)Math.Round((now.X - grab.X) * scaling),
            Position.Y + (int)Math.Round((now.Y - grab.Y) * scaling));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_placing)
        {
            return;
        }

        e.Pointer.Capture(null);
        Settle();
    }

    /// <summary>
    /// Ends place mode: the pointer goes back through, the border goes away, and where the strip ended
    /// up is written down.
    /// </summary>
    private void Settle()
    {
        _placing = false;
        _grab = null;

        _frame.BorderThickness = new Thickness(0);

        ApplyStyles();
        Remember();
    }

    /// <summary>
    /// Read-modify-write against the file rather than against a snapshot, exactly as the window's own
    /// placement and the VR anchors do: the settings page writes card state into the same store while
    /// this is open, and saving a stale copy would silently undo it.
    /// </summary>
    private void Remember()
    {
        // From here the corner is theirs, and nothing picks one again (#36).
        _placed = true;

        var placement = new OverlayPlacement { X = Position.X, Y = Position.Y };

        _viewState.Save(_viewState.Load().With(placement));

        _logger.LogDebug(
            "The overlay was left at {X},{Y}",
            Position.X.ToString(CultureInfo.InvariantCulture),
            Position.Y.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// This window's Win32 handle, or null where there is not one — which is Avalonia's headless
    /// platform, where the tests run.
    /// </summary>
    private IntPtr? Handle() =>
        TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero ? handle : null;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, int flags);
}
