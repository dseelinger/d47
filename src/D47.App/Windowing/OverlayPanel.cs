using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
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
/// The mini panel without a headset: a chromeless, click-through strip pinned over the game
/// (list.md Phase 48).
/// <para>
/// <b>A third instantiation, not a second design.</b> <see cref="PanelView"/> is a
/// <c>UserControl</c> instantiated once per surface against one <see cref="PanelViewModel"/>, and
/// mini is <see cref="PanelMode.Mini"/> — a reduced content set rather than a smaller copy. So
/// this is a third host calling the same constructor with the same model and the same mode. There
/// is no second view definition, no screenshot of the window, and nothing to keep in step: the
/// overlay cannot show something stale because there is nothing for it to be stale about.
/// </para>
/// <para>
/// <b>The Phase 9 trap does not reach here.</b> A <c>UserControl</c> detached from a logical tree
/// rasterises as an empty quad with no error, which is why <c>VrPanelSurface</c> hosts its copy in
/// a <see cref="Window"/> that is constructed and never shown. This one is a window that <em>is</em>
/// shown, which is the ordinary path and the easy half. What does reach here is the rule that
/// amendment protects — the VR path never depends on the state of the window the Commander can see
/// — and a third surface makes that three independent facts rather than two.
/// </para>
/// <para>
/// <b>Output-only, and the pointer goes straight through it.</b> A click the overlay eats is a
/// click Elite did not get, and a focus steal mid-combat is worse than anything the overlay could
/// have been showing. Three window styles carry that and each is its own claim — see
/// <see cref="ApplyStyles"/>. The one exception is placement, which has to be explicit:
/// <see cref="Place"/> briefly takes clicks so the strip can be dragged, and gives them back the
/// moment it is done.
/// </para>
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

    /// <summary>
    /// Whether Elite has the foreground, as this surface asks it. A function rather than the
    /// window itself, so the one question this needs is the whole of what it depends on — and so
    /// the surface built by a caller that has no game to ask about answers "not in front", which
    /// is the honest default and also the one that draws nothing.
    /// </summary>
    private Func<bool> _eliteInFront = () => false;

    private bool _placing;
    private Point? _grab;
    private int _appliedScale = ZoomLadder.Default;

    public OverlayPanel(
        PanelViewModel model,
        SettingsService settings,
        ViewStateStore viewState,
        ILogger logger,
        AvatarLibrary? avatars = null,
        AdventureSurface? adventures = null)
    {
        _settings = settings;
        _viewState = viewState;
        _logger = logger;

        _view = new PanelView { DataContext = model, Mode = PanelMode.Mini };
        _view.Avatar.Library = avatars;

        if (adventures is not null)
        {
            // The story, at mini's size (list.md Phase 48). <b>These two roots are all the overlay
            // furnishes</b> — the transcript, which every surface has by construction, and this.
            // Being sparse costs no special case anywhere: `PanelView.Tab` already declines to
            // select a tab nobody furnished, which is the same *not calling `Furnish`* that
            // withdrew Loadout from the headset.
            _view.EnableAdventures(adventures);
        }

        // The same scaling host the window zooms with, for the same reason: a render transform
        // would draw the strip larger and let it clip, where a layout transform re-measures so
        // text rewraps and spacing grows with it. Scale is the lever here because there are no
        // metres — see OverlaySettings.ScalePercent.
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

        // The panel paints its own background over every pixel of this window, so what this brush
        // is is only ever seen for the width of a layout pass. It is bound anyway rather than left
        // at the platform's white: the one frame between opening and the first paint is a frame a
        // Commander sees, and a white flash over a cockpit is the most visible thing d47 could do.
        this.Bind(BackgroundProperty, this.GetResourceObservable(Theming.ThemeManager.BackgroundKey));

        ApplyScale();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    /// <summary>
    /// Where this surface is, so a spoken phrase and a switch can move it (list.md Phase 45 and
    /// Phase 46). It joins through the same <c>AppHost.RouteNavigation</c> that already routes the
    /// other two, and never as the leader: the window leads and this follows, so the overlay's tab
    /// can never drag the window's.
    /// </summary>
    public PanelNavigator Nav => _view.Nav;

    /// <summary>Whether the strip is in place mode — taking clicks so it can be dragged.</summary>
    public bool IsPlacing => _placing;

    /// <summary>
    /// Whether the strip should be on screen at all.
    /// <para>
    /// <b>Visible when Elite is in front and hidden otherwise.</b> A strip pinned over a browser
    /// is a strip the Commander turns off within a day.
    /// <see cref="IEliteWindow.IsForeground"/> is the existing answer and is already cheap enough
    /// to be asked before every injected key, so it is asked on the tick like everything else that
    /// reads the world.
    /// </para>
    /// <para>
    /// <b>It hides when d47's own window is in front</b>, because the panel is right there showing
    /// strictly more. That is a decision rather than an extra term: Elite does not hold the
    /// foreground while d47 does, so declining to carve out an exception is the whole of it.
    /// </para>
    /// <para>
    /// <b>And there is no interlock with the headset.</b> A Commander in VR has no use for this,
    /// but wanting both is a real case — a second monitor somebody else is watching — and an
    /// overlay that silently declined to appear because SteamVR was running would be exactly the
    /// undiagnosable nothing this feature's display check exists to prevent. Off by default, which
    /// settles it either way.
    /// </para>
    /// <para>
    /// Placing wins over all of it. A Commander sets the strip up before launching the game as
    /// often as after, and a place mode that could only be entered with Elite already in front
    /// would be one they cannot reach on the day they need it.
    /// </para>
    /// </summary>
    public static bool ShouldShow(bool enabled, bool eliteForeground, bool placing) =>
        enabled && (placing || eliteForeground);

    /// <summary>
    /// Builds the overlay, applies the remembered placement and subscribes it to the tick and to
    /// settings.
    /// <para>
    /// Constructed unconditionally, whether or not the Commander has turned it on — the same
    /// bargain the headset path strikes, and for the same reason: a code path that only runs for
    /// people who enabled something is the code path that breaks.
    /// </para>
    /// </summary>
    public static OverlayPanel Attach(
        PanelViewModel model,
        SettingsService settings,
        ViewStateStore viewState,
        TickLoop tick,
        IEliteWindow elite,
        ILogger logger,
        AvatarLibrary? avatars = null,
        AdventureSurface? adventures = null)
    {
        var overlay = new OverlayPanel(model, settings, viewState, logger, avatars, adventures)
        {
            _eliteInFront = () => elite.IsForeground,
        };

        overlay.Restore();
        overlay.ApplyVisibility();

        // Captured on this surface's own thread rather than read from the static one at call
        // time. That is the headless-dispatcher rule, and it is not optional on a surface whose
        // whole job is drawing what another thread changed.
        var ui = Dispatcher.UIThread;

        settings.Changed += change => ui.Post(() => overlay.Configure(change.Key));

        // Polled on the tick, like everything else that reads the world. The foreground question
        // is a syscall and the rest is a comparison, so a tick where nothing moved costs nothing.
        tick.Add("overlay", _ => ui.Post(overlay.ApplyVisibility));

        return overlay;
    }

    /// <summary>
    /// Hands the pointer back to the strip so it can be dragged, and takes it away again the
    /// moment the Commander lets go (list.md Phase 48).
    /// <para>
    /// Reached by a system-wide gesture rather than by a settings row, because the overlay is
    /// hidden whenever d47's own window is the thing in front — so a button on a page is a button
    /// that cannot be pressed while the thing it moves is on screen. The headset's answer to the
    /// same question is the re-anchor gesture, for the same reason.
    /// </para>
    /// <para>
    /// <c>WS_EX_NOACTIVATE</c> stays on throughout, so even a drag never takes the foreground from
    /// Elite. Only the pass-through comes off.
    /// </para>
    /// </summary>
    public void Place()
    {
        if (_placing)
        {
            // A second press with nothing dragged is the Commander changing their mind, which is
            // the only reading of it that means anything.
            Settle();
            return;
        }

        _placing = true;

        // Shown before the styles are re-applied, because there is no window handle to set an
        // extended style on until there is a window. Placing wins in ShouldShow, so this brings
        // the strip up whether or not the game is running.
        ApplyVisibility();
        ApplyStyles();

        _frame.BorderThickness = new Thickness(2);
        _frame.Bind(Border.BorderBrushProperty, this.GetResourceObservable(Theming.ThemeManager.AccentKey));

        _logger.LogInformation("The overlay is in place mode; drag it and let go");
    }

    /// <summary>
    /// Re-reads whatever the changed row means for this surface. One method rather than a handler
    /// per row, because they are four readings of one state.
    /// </summary>
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

    /// <summary>
    /// Opens or closes the strip. Hiding it also ends a placement in progress: a Commander who
    /// turned the overlay off while dragging it meant the first thing.
    /// </summary>
    private void ApplyVisibility()
    {
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

        Show();

        // After the window exists, which is the first moment there is a handle to style. Repeated
        // on every show rather than done once: a window that has been hidden and shown again is
        // not guaranteed to be the same handle, and setting an extended style twice costs nothing.
        ApplyStyles();
        ApplyOpacity();
    }

    /// <summary>
    /// The three extended styles, each of them its own claim about what this surface is.
    /// <para>
    /// <c>WS_EX_TRANSPARENT</c> so the pointer passes through, <c>WS_EX_NOACTIVATE</c> so it never
    /// takes the foreground, and <c>WS_EX_TOOLWINDOW</c> so it is not something to Alt-Tab into.
    /// The first comes off in place mode and goes straight back on; the other two never do.
    /// </para>
    /// <para>
    /// Fails soft on a platform with no Win32 handle — Avalonia's headless one, which is where the
    /// tests run. What is lost there is the click-through, which a headless surface has nobody to
    /// click it with anyway.
    /// </para>
    /// </summary>
    private void ApplyStyles()
    {
        if (!OperatingSystem.IsWindows() || Handle() is not { } handle)
        {
            return;
        }

        try
        {
            var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();

            // Layered too, because that is what lets the opacity row mean anything: an alpha
            // set with SetLayeredWindowAttributes is applied against what is behind the window,
            // which is the game. See ApplyOpacity.
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
            // The 32-bit entry point is SetWindowLongW and d47 is x64, so this is unreachable in
            // any shipped build - but a swallowed style is a strip that eats clicks, which is the
            // one failure this surface must not have quietly.
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

        // The headset's mini panel is fixed at 512x280 because apparent size there is the pixel
        // count and the quad's width in metres together. On a monitor half of that product does
        // not exist, so the pixel size falls out of the ladder instead — and because the scale is
        // a layout transform, a bigger strip is a re-wrapped strip rather than a blurred one.
        Width = PanelResolution.Mini.Width * factor;
        Height = PanelResolution.Mini.Height * factor;

        // The remembered corner is the top-left, so a strip that grew still starts where it was
        // put and is pushed back on screen only if growing took it off.
        Clamp();
    }

    /// <summary>
    /// How much cockpit shows through, as one alpha over the whole strip.
    /// <para>
    /// <b>Through Win32 rather than through <see cref="Visual.Opacity"/></b>, and the difference is
    /// not cosmetic. Avalonia's opacity blends the content against the window's <em>own</em>
    /// background, which here is the same colour it is drawing — so a Commander who asked for half
    /// would get a washed-out strip that still hid exactly as much cockpit as before. A layered
    /// window's alpha is applied by the compositor against whatever is behind the window, which is
    /// the game, which is the thing the setting is about.
    /// </para>
    /// <para>
    /// Uniform rather than per-pixel: <c>SetLayeredWindowAttributes</c> and not
    /// <c>UpdateLayeredWindow</c>, so Avalonia goes on drawing the window the ordinary way and
    /// nothing here has to own a frame.
    /// </para>
    /// </summary>
    private void ApplyOpacity()
    {
        var solid = Math.Clamp(_settings.Current.Ui.Overlay.Opacity, 0.2, 1);

        if (!OperatingSystem.IsWindows() || Handle() is not { } handle)
        {
            return;
        }

        SetLayeredWindowAttributes(handle, 0, (byte)Math.Round(solid * 255), LwaAlpha);
    }

    /// <summary>
    /// Where the Commander left it, or the bottom-right of the screen in front of them.
    /// <para>
    /// Bottom-right because that is the corner of Elite's HUD with the least on it, and because a
    /// strip that opens over the compass is a strip that gets moved before it gets read.
    /// </para>
    /// </summary>
    private void Restore()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (_viewState.Load().Overlay is { } placement)
        {
            Position = new PixelPoint((int)placement.X, (int)placement.Y);
            Clamp();
            return;
        }

        var screen = Screens?.Primary ?? Screens?.All.FirstOrDefault();

        if (screen is null)
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
    /// Pushes the strip back onto a screen it is at least partly on. A remembered position on a
    /// monitor that has since been unplugged is a strip the Commander cannot see and cannot move,
    /// which reads exactly like the setting not working.
    /// </summary>
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

        var area = (screens.Primary ?? screens.All[0]).WorkingArea;

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

    /// <summary>
    /// Moves the window by however far the pointer has travelled since it was grabbed.
    /// <para>
    /// The grab point stays valid as the window moves, because it is measured inside the window:
    /// moving the frame under a stationary pointer puts the pointer back where it was grabbed.
    /// </para>
    /// </summary>
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
    /// Ends place mode: the pointer goes back through, the border goes away, and where the strip
    /// ended up is written down.
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
    /// Read-modify-write against the file rather than against a snapshot, exactly as the window's
    /// own placement and the VR anchors do: the settings page writes card state into the same
    /// store while this is open, and saving a stale copy would quietly undo it.
    /// </summary>
    private void Remember()
    {
        var placement = new OverlayPlacement { X = Position.X, Y = Position.Y };

        _viewState.Save(_viewState.Load().With(placement));

        _logger.LogDebug(
            "The overlay was left at {X},{Y}",
            Position.X.ToString(CultureInfo.InvariantCulture),
            Position.Y.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// This window's Win32 handle, or null where there is not one — which is Avalonia's headless
    /// platform, where the tests run. Everything that reads it fails soft: what is lost there is
    /// the click-through and the alpha, neither of which a headless surface has anybody to notice.
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
