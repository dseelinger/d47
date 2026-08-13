using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
    private readonly SettingsService _settings;
    private readonly Func<string, (VrPose Placed, VrPose Against)?> _anchor;
    private readonly PanelView _view;
    private readonly ScaleTransform _scale = new();
    private readonly OffscreenSurface _offscreen;

    private bool _dirty = true;
    private int _appliedZoom = ZoomLadder.Default;
    private VrPose _head = VrPose.Origin;

    public VrPanelSurface(
        PanelViewModel model,
        SettingsService settings,
        Func<string, (VrPose Placed, VrPose Against)?> anchor)
    {
        _model = model;
        _settings = settings;
        _anchor = anchor;

        _view = new PanelView { DataContext = model };

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
        _offscreen.Render();
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;
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

    public void Dispose()
    {
        _model.PropertyChanged -= OnModelChanged;
        _offscreen.Dispose();
    }

    private VrSurfaceSettings Settings() =>
        Mode == PanelMode.Mini ? _settings.Current.Vr.Mini : _settings.Current.Vr.Panel;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
}
