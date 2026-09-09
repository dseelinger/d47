using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Panel;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The panel, as the headset needs it: a second instantiation of <see cref="PanelView"/> bound to the
/// view model the desktop window is already showing, rasterised offscreen.
/// </summary>
public sealed class VrPanelSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>
    /// Mini, and it is 512 wide rather than 640 because that is the lever on apparent text size.
    /// </summary>
    private static readonly PixelSize Mini =
        new(PanelResolution.Mini.Width, PanelResolution.Mini.Height);

    private readonly PanelViewModel _model;
    private readonly string? _dumpTo;

    private bool _kept;
    private readonly SettingsService _settings;
    private readonly Func<string, (VrPose Placed, VrPose Against)?> _anchor;
    private readonly PanelView _view;
    private readonly ScaleTransform _scale = new();
    private readonly OffscreenSurface _offscreen;

    private bool _dirty = true;
    private int _appliedZoom = ZoomLadder.Default;
    private (int Width, int Height) _appliedPixels = PanelResolution.Default;
    private VrPose _head = VrPose.Origin;

    /// <summary>
    /// <param name="settingsPage"> Builds the settings surface for this copy of the panel, or null to
    /// leave it without one.
    /// </summary>
    /// <param name="settingsPage">
    /// Builds the settings surface for this copy of the panel, or null to leave it without one.
    /// </param>
    public VrPanelSurface(
        PanelViewModel model,
        SettingsService settings,
        Func<string, (VrPose Placed, VrPose Against)?> anchor,
        D47.Core.Interface.AvatarLibrary? avatars = null,
        string? dumpTo = null,
        Func<Control>? settingsPage = null,
        D47.Core.Checklists.ChecklistService? checklists = null,
        D47.Core.Utilities.Timekeeper? timekeeper = null,
        D47.Core.Utilities.AlarmStore? alarmStore = null,
        D47.Core.Ships.ShipPlanService? ships = null,
        Func<D47.Core.Journal.CommanderGameState?>? gameState = null,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        D47.Core.Engineers.EngineerPlanService? unlocks = null,

        // The Commander's long arcs, and the button that ages them (Phase 34).
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfillGoals = null,

        // The stories the Commander flies (Phase 47), in the headset from 2026-08-22.
        Panel.AdventureSurface? adventures = null,

        // Where the headset was left, across launches (#276) — null in every test that has no opinion about
        // it, which leaves this surface exactly as it always behaved.
        ViewStateStore? viewState = null)
    {
        _dumpTo = dumpTo;

        _model = model;
        _settings = settings;
        _anchor = anchor;

        _view = new PanelView { DataContext = model };

        // The Commander's own avatar frames reach the headset copy too.
        _view.Avatar.Library = avatars;

        if (settingsPage is not null)
        {
            _view.EnableSettings(settingsPage);
        }

        if (checklists is not null)
        {
            // What the Commander is working on, back in the headset (Phase 39).
            _view.EnableChecklist(checklists, goals, backfillGoals);
        }

        // The journal's raw reading, in the headset (#231).
        _view.EnableRawJournal();

        if (adventures is not null)
        {
            // The stories, in the headset (asked for 2026-08-22).
            _view.EnableAdventures(adventures);
        }

        // `ships`, `gameState` and `onFoot` are still read below - Engineers needs all three.

        if (unlocks is not null && ships is not null && gameState is not null)
        {
            // And who to go and get next (Phase 28).
            _view.EnableEngineers(unlocks, ships, gameState, onFoot);
        }

        if (timekeeper is not null && alarmStore is not null)
        {
            // A Commander in a headset is exactly the Commander who cannot glance at a wall clock, which is
            // most of why this page exists at all (Phase 24).
            _view.EnableUtilities(
                timekeeper,
                alarmStore,
                () => D47.Core.SystemWallClock.Instance.UtcNow,
                () => TimeZoneInfo.Local);
        }

        if (viewState is not null)
        {
            // The headset's own tab and roots, back where they were left (#276).
            _view.RememberRoots(new PanelRootMemory(viewState, vr: true));
            _view.RememberTab(new PanelTabMemory(viewState, vr: true));
        }

        // The same scaling host the desktop window zooms with, for the same reason: a render transform would
        // draw the panel larger and let the surface clip it, where a layout transform re-measures so text
        // rewraps and spacing grows with it. "Scale the big panel" and "Zoom the desktop window" are one
        // mechanism seen from two rooms.
        var pixels = settings.Current.Vr.Panel.Resolution;

        _offscreen = new OffscreenSurface(
            new LayoutTransformControl { LayoutTransform = _scale, Child = _view },
            new PixelSize(pixels.Width, pixels.Height));

        // Anything the panel shows changing is a reason to redraw, and nothing else is.
        model.PropertyChanged += OnModelChanged;
    }

    public bool Enabled { get; set; }

    /// <summary>This surface's own prompts (Phase 25).</summary>
    public Panel.PanelPrompts Prompts => _view.Prompts;

    /// <summary>
    /// Back one level on this surface, and whether there was anything to go back from — so the
    /// controller button stays available to whatever else wants it at a root (Phase 25).
    /// </summary>
    public bool Back() => _view.GoBack();

    /// <summary>Redraws the clocks, from the headset's own tick.</summary>
    public void TickClocks() => _dirty |= _view.TickClocks();

    /// <summary>
    /// Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody (Phase
    /// 28).
    /// </summary>
    public void TickEngineers() => _dirty |= _view.TickEngineers();

    /// <summary>One frame of the d47 is composing animation (asked for 2026-08-22).</summary>
    public void TickAdventures() => _dirty |= _view.TickAdventures();

    /// <summary>Where this surface currently is, for a spoken phrase to move.</summary>
    public D47.Core.Interface.PanelNavigator Nav => _view.Nav;

    /// <summary>Moves the page this surface is showing, for a spoken scroll (#34).</summary>
    public D47.Core.Interface.PanelScrollOutcome Scroll(D47.Core.Interface.PanelScrollStep step)
    {
        var outcome = _view.Scroll(step);

        // Dirty only for a move, which is the same bargain as before and is now said precisely: "already at
        // the bottom" changes no pixel, and re-rendering for it would hand SteamVR an identical image (#263).
        _dirty |= outcome == D47.Core.Interface.PanelScrollOutcome.Moved;

        return outcome;
    }

    /// <summary>Which mode the Commander has the panel in.</summary>
    public PanelMode Mode =>
        string.Equals(_settings.Current.Vr.Mode, "mini", StringComparison.OrdinalIgnoreCase)
            ? PanelMode.Mini
            : PanelMode.Full;

    public VrSurface Surface => Mode == PanelMode.Mini ? VrSurface.PanelMini : VrSurface.PanelFull;

    public bool Visible => Enabled;

    /// <summary>
    /// The panel is the surface a hand can do something to — it is grab-to-move — so it is the one that
    /// asks SteamVR for a laser and the mouse events that come back with it.
    /// </summary>
    public bool TakesPointer => true;

    /// <summary>Where this surface goes and what it looks like.</summary>
    public SurfacePlacement Placement
    {
        get
        {
            var placement = Settings().ToPlacement(_settings.Current.Vr.Opacity);

            return _anchor(Slot) is { } anchor
                ? placement with { Placed = anchor.Placed, PlacedAgainst = anchor.Against }
                : placement;
        }
    }

    /// <summary>Which settings slot this mode reads from.</summary>
    public string Slot => Mode == PanelMode.Mini
        ? D47.Core.Capabilities.Builtin.VrCapability.MiniSlot
        : D47.Core.Capabilities.Builtin.VrCapability.PanelSlot;

    /// <summary>
    /// How many pixels to render, which is the Commander's since Phase 25 for the big panel and fixed
    /// for mini.
    /// </summary>
    public (int Width, int Height) Size =>
        Mode == PanelMode.Mini ? (Mini.Width, Mini.Height) : Settings().Resolution;

    public bool IsDirty => _dirty;

    /// <summary>Where the head was when this surface was last served.</summary>
    public VrPose Head => _head;

    public void Observe(VrPose head) => _head = head;

    /// <summary>
    /// Which way <c>output-only</c> was last set, so the class is touched when the mode changes and not
    /// once a frame.
    /// </summary>
    private bool? _outputOnly;

    /// <summary>Mini carries no buttons, the same as the flat overlay (change-requests.md 42).</summary>
    private void KeepChromeInStep()
    {
        var outputOnly = Mode == PanelMode.Mini;

        if (_outputOnly == outputOnly)
        {
            return;
        }

        _outputOnly = outputOnly;
        _view.Classes.Set("output-only", outputOnly);
    }

    public void Draw(IntPtr destination, int rowBytes)
    {
        KeepChromeInStep();

        var (width, height) = Size;
        _offscreen.Resize(new PixelSize(width, height));
        // Following is re-asserted between the layout and the rasterise, because that is the one moment this
        // tree has a real extent to scroll to the end of.
        var rendered = _offscreen.Render(_view.KeepUp);
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;

        Keep(rendered);
    }

    /// <summary>
    /// Writes the first frame of a session to <c>data/</c>, so what the headset was handed can be
    /// looked at rather than reasoned about.
    /// </summary>
    private void Keep(RenderTargetBitmap rendered)
    {
        if (_kept || _dumpTo is not { } folder)
        {
            return;
        }

        _kept = true;

        try
        {
            rendered.Save(
                Path.Combine(folder, $"vr-{Surface}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A diagnostic must never be why a frame fails.
            _ = ex;
        }
    }

    /// <summary>Re-reads the settings that change what is drawn rather than where it goes.</summary>
    public void Configure()
    {
        // Two levers, both read here and both only marking dirty when they actually moved: this runs on every
        // tick of a live session, and a surface held dirty for a setting nobody touched re-renders the whole
        // widget tree at frame rate for pixels that did not change.
        var pixels = Size;

        if (pixels != _appliedPixels)
        {
            _appliedPixels = pixels;
            _dirty = true;
        }

        var zoom = ZoomLadder.Snap(Settings().Zoom);

        if (zoom == _appliedZoom)
        {
            return;
        }

        _appliedZoom = zoom;
        _scale.ScaleX = ZoomLadder.ScaleOf(zoom);
        _scale.ScaleY = ZoomLadder.ScaleOf(zoom);
        _dirty = true;
    }

    /// <summary>The mode this surface shows, pushed onto its own view rather than onto the model.</summary>
    public void ApplyMode()
    {
        if (_view.Mode == Mode)
        {
            return;
        }

        _view.Mode = Mode;
        _dirty = true;
    }

    /// <summary>Forces the next serve to redraw — after a reconnect, or a mode change.</summary>
    public void Invalidate() => _dirty = true;

    /// <summary>
    /// A press at a point on the quad's face, in the 0..1 the ray already answers in, and whether there
    /// was anything there to press.
    /// </summary>
    public bool Press(float u, float v)
    {
        var (width, height) = Size;
        var landed = _offscreen.Click(new Point(u * width, v * height));

        _dirty = true;

        return landed;
    }

    /// <summary>
    /// Takes hold of the scrollbar a ray is aiming at, if there is one within reach, and says whether
    /// it did.
    /// </summary>
    public bool GrabsScroll(float u, float v)
    {
        var (width, height) = Size;
        var at = new Point(u * width, v * height);

        _scrolling = _offscreen.ScrollbarNear(at);

        if (_scrolling is null)
        {
            return false;
        }

        Scroll(u, v);
        return true;
    }

    /// <summary>Moves the held bar to where the ray is now.</summary>
    public void Scroll(float u, float v)
    {
        if (_scrolling is null)
        {
            return;
        }

        var (width, height) = Size;

        OffscreenSurface.Aim(_scrolling, _offscreen.View, new Point(u * width, v * height));
        _dirty = true;
    }

    /// <summary>Lets go.</summary>
    public void ReleaseScroll() => _scrolling = null;

    /// <summary>Lights whatever the ray is resting on, so the Commander can see they have found it.</summary>
    public void Aim(float? u, float? v)
    {
        // Held beats aimed.
        if (_scrolling is not null)
        {
            _dirty |= _offscreen.Illuminate(_scrolling);
            return;
        }

        if (u is not { } across || v is not { } down)
        {
            _dirty |= _offscreen.Illuminate(null);
            return;
        }

        var (width, height) = Size;
        var lit = _offscreen.ScrollbarNear(new Point(across * width, down * height));

        _dirty |= _offscreen.Illuminate(lit);
    }

    private Avalonia.Controls.Primitives.ScrollBar? _scrolling;

    public void Dispose()
    {
        _model.PropertyChanged -= OnModelChanged;
        _offscreen.Dispose();
    }

    private VrSurfaceSettings Settings() =>
        Mode == PanelMode.Mini ? _settings.Current.Vr.Mini : _settings.Current.Vr.Panel;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
}
