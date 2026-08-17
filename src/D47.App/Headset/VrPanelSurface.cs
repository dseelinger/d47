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
/// The panel, as the headset needs it: a second instantiation of <see cref="PanelView"/> bound
/// to the view model the desktop window is already showing, rasterised offscreen.
/// <para>
/// This is where "one widget tree renders to both surfaces" actually lands. There is no second
/// view definition and no screenshot of the window — both surfaces read one model, so the
/// windowed one cannot be more functional than the headset one by construction rather than by
/// anybody remembering (list.md Phase 9).
/// </para>
/// </summary>
public sealed class VrPanelSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>
    /// The panel's pixels, per mode. Sized deliberately: cost is linear in pixels, and there is
    /// no reason to render more of them than the quad subtends. Mini is a genuinely smaller
    /// image rather than the same one drawn small, because apparent text size in a headset is
    /// pixel count and metres together.
    /// </summary>
    private static readonly PixelSize Full = new(1024, 640);

    private static readonly PixelSize Mini = new(640, 280);

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
    private VrPose _head = VrPose.Origin;

    /// <param name="settingsPage">
    /// Builds the settings surface for this copy of the panel, or null to leave it without one.
    /// <para>
    /// It used to be structurally absent here, on the reasoning that a nav column beside a
    /// 700-pixel minimum has no business on a quad a metre away. The quad is 1024 wide and the
    /// surface already collapses its nav below 900, so what it renders at is the arrangement a
    /// Commander sees in the desktop window at its default size — and every reason to reach a
    /// setting applies at least as much with a headset on, where there is no other way to reach
    /// one (remediation.md, "The VR big panel should carry the Settings tab").
    /// </para>
    /// </param>
    public VrPanelSurface(
        PanelViewModel model,
        SettingsService settings,
        Func<string, (VrPose Placed, VrPose Against)?> anchor,
        D47.Core.Interface.AvatarLibrary? avatars = null,
        string? dumpTo = null,
        Func<Control>? settingsPage = null)
    {
        _dumpTo = dumpTo;

        _model = model;
        _settings = settings;
        _anchor = anchor;

        _view = new PanelView { DataContext = model };

        // The Commander's own avatar frames reach the headset copy too. One widget tree renders
        // to both surfaces, and a face the window has and the headset does not would be exactly
        // the parity the one-widget-tree item exists to protect.
        _view.Avatar.Library = avatars;

        if (settingsPage is not null)
        {
            _view.EnableSettings(settingsPage);
        }

        // The same scaling host the desktop window zooms with, for the same reason: a render
        // transform would draw the panel larger and let the surface clip it, where a layout
        // transform re-measures so text rewraps and spacing grows with it. "Scale the big
        // panel" and "Zoom the desktop window" are one mechanism seen from two rooms.
        _offscreen = new OffscreenSurface(
            new LayoutTransformControl { LayoutTransform = _scale, Child = _view },
            Full);

        // Anything the panel shows changing is a reason to redraw, and nothing else is. This
        // is D1's second Phase 9 instruction in one line: the measured 4-10 Hz cost is the
        // worst case, and a panel with nothing new costs a boolean.
        model.PropertyChanged += OnModelChanged;
    }

    public bool Enabled { get; set; }

    /// <summary>Which mode the Commander has the panel in. Read from settings, never held here.</summary>
    public PanelMode Mode =>
        string.Equals(_settings.Current.Vr.Mode, "mini", StringComparison.OrdinalIgnoreCase)
            ? PanelMode.Mini
            : PanelMode.Full;

    public VrSurface Surface => Mode == PanelMode.Mini ? VrSurface.PanelMini : VrSurface.PanelFull;

    public bool Visible => Enabled;

    /// <summary>
    /// The panel is the surface a hand can do something to — it is grab-to-move — so it is the
    /// one that asks SteamVR for a laser and the mouse events that come back with it.
    /// </summary>
    public bool TakesPointer => true;

    /// <summary>
    /// Where this surface goes and what it looks like. The look comes from settings and the
    /// anchor from view state, and they are joined here rather than in either store: choosing
    /// to be world-locked is a preference, and where a hand happened to leave it is not.
    /// </summary>
    public SurfacePlacement Placement
    {
        get
        {
            var placement = Settings().ToPlacement();

            return _anchor(Slot) is { } anchor
                ? placement with { Placed = anchor.Placed, PlacedAgainst = anchor.Against }
                : placement;
        }
    }

    /// <summary>Which settings slot this mode reads from.</summary>
    public string Slot => Mode == PanelMode.Mini
        ? D47.Core.Capabilities.Builtin.VrCapability.MiniSlot
        : D47.Core.Capabilities.Builtin.VrCapability.PanelSlot;

    public (int Width, int Height) Size
    {
        get
        {
            var wanted = Mode == PanelMode.Mini ? Mini : Full;
            return (wanted.Width, wanted.Height);
        }
    }

    public bool IsDirty => _dirty;

    /// <summary>Where the head was when this surface was last served. Re-anchor reads it.</summary>
    public VrPose Head => _head;

    public void Observe(VrPose head) => _head = head;

    public void Draw(IntPtr destination, int rowBytes)
    {
        var (width, height) = Size;
        _offscreen.Resize(new PixelSize(width, height));
        // Following is re-asserted between the layout and the rasterise, because that is the one
        // moment this tree has a real extent to scroll to the end of.
        var rendered = _offscreen.Render(_view.KeepUp);
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;

        Keep(rendered);
    }

    /// <summary>
    /// Writes the first frame of a session to <c>data/</c>, so what the headset was handed can
    /// be looked at rather than reasoned about.
    /// <para>
    /// The rasterise is covered by a test, but that test runs on Avalonia's headless platform
    /// and this runs on Win32 against a window that is never shown. That difference is the last
    /// thing between the two that has never been observed in a real build, and every other
    /// explanation for an overlay SteamVR reports as visible has been wrong.
    /// </para>
    /// <para>
    /// Once per session and overwritten each time, so it stays one small file rather than a
    /// stream of them, and it never touches the frame path after the first.
    /// </para>
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

    /// <summary>
    /// Re-reads the settings that change what is drawn rather than where it goes. Placement is
    /// read fresh on every serve and needs nothing; the content scale changes the render, so it
    /// has to invalidate.
    /// </summary>
    public void Configure()
    {
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

    /// <summary>
    /// The mode this surface shows, pushed onto its own view rather than onto the model. The
    /// desktop window is always the full panel and the headset follows the setting; a mode held
    /// on the shared model would put the window into mini the moment the headset went into it.
    /// </summary>
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
    /// A press at a point on the quad's face, in the 0..1 the ray already answers in, and whether
    /// there was anything there to press.
    /// <para>
    /// The conversion to pixels is the whole of what this adds: <see cref="VrHit"/> is a fraction
    /// across and down the face in raster order, and the view is laid out at whatever
    /// <see cref="Size"/> the current mode asks for. Zoom needs no part in it — the scale is a
    /// layout transform inside the surface, so the view's own coordinate space is the pixel space
    /// either way.
    /// </para>
    /// <para>
    /// The frame after a press is a different frame, so it is marked dirty unconditionally: a tab
    /// that has been selected and not redrawn is a tab a Commander pressed twice.
    /// </para>
    /// </summary>
    public bool Press(float u, float v)
    {
        var (width, height) = Size;
        var landed = _offscreen.Click(new Point(u * width, v * height));

        _dirty = true;

        return landed;
    }

    /// <summary>
    /// Takes hold of the scrollbar a ray is aiming at, if there is one within reach, and says
    /// whether it did.
    /// <para>
    /// Asked before a carry is allowed to begin, so a hand that came down on a scrollbar scrolls
    /// rather than picking the whole panel up. The two gestures are the same button and cannot
    /// both run.
    /// </para>
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

    /// <summary>Lets go. The bar stays where it was left.</summary>
    public void ReleaseScroll() => _scrolling = null;

    /// <summary>
    /// Lights whatever the ray is resting on, so the Commander can see they have found it.
    /// Called every frame a ray is on the panel, and with null when it leaves.
    /// </summary>
    public void Aim(float? u, float? v)
    {
        if (u is not { } across || v is not { } down)
        {
            _offscreen.Illuminate(null);
            _dirty = true;
            return;
        }

        var (width, height) = Size;
        var lit = _offscreen.ScrollbarNear(new Point(across * width, down * height));

        _offscreen.Illuminate(lit);
        _dirty = true;
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
