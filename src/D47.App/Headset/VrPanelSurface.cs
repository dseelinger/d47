using System.ComponentModel;
using Avalonia;
using D47.App.Panel;
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
    private readonly PanelView _view;
    private readonly OffscreenSurface _offscreen;
    private readonly Func<PanelMode, SurfacePlacement> _placement;

    private bool _dirty = true;
    private VrPose _head = VrPose.Origin;

    public VrPanelSurface(PanelViewModel model, Func<PanelMode, SurfacePlacement> placement)
    {
        _model = model;
        _placement = placement;

        _view = new PanelView { DataContext = model };
        _offscreen = new OffscreenSurface(_view, Full);

        // Anything the panel shows changing is a reason to redraw, and nothing else is. This
        // is D1's second Phase 9 instruction in one line: the measured 4-10 Hz cost is the
        // worst case, and a panel with nothing new costs a boolean.
        model.PropertyChanged += OnModelChanged;
    }

    public bool Enabled { get; set; }

    public VrSurface Surface => _model.Mode == PanelMode.Mini ? VrSurface.PanelMini : VrSurface.PanelFull;

    public bool Visible => Enabled;

    public SurfacePlacement Placement => _placement(_model.Mode);

    public (int Width, int Height) Size
    {
        get
        {
            var wanted = _model.Mode == PanelMode.Mini ? Mini : Full;
            return (wanted.Width, wanted.Height);
        }
    }

    public bool IsDirty => _dirty;

    public void Observe(VrPose head) => _head = head;

    /// <summary>Where the head was when this surface was last served, for re-anchoring.</summary>
    public VrPose Head => _head;

    public void Draw(IntPtr destination, int rowBytes)
    {
        var (width, height) = Size;
        _offscreen.Resize(new PixelSize(width, height));
        _offscreen.Render();
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;
    }

    /// <summary>Forces the next serve to redraw — after a zoom change, or a reconnect.</summary>
    public void Invalidate() => _dirty = true;

    public void Dispose()
    {
        _model.PropertyChanged -= OnModelChanged;
        _offscreen.Dispose();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
}
