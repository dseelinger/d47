using System.ComponentModel;
using Avalonia;
using D47.App.Panel;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The caption quad (list.md Phase 9, "TheApp appears in the headset").
/// <para>
/// Its own overlay handle, head-locked, flat, and unmovable. Not a preference — a caption the
/// Commander can drag is a caption they can drag somewhere they will not see it, and the one
/// thing a caption has to be is where they are already looking. Being a separate handle is
/// also what keeps <em>Overlay Positioning &amp; Look</em> from reaching it by accident.
/// </para>
/// </summary>
public sealed class VrCaptionSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>
    /// Wide and short: three lines of forty-two characters and nothing else. The height carries
    /// the box's padding as well as the text, so it is not simply three times a line.
    /// </summary>
    private static readonly PixelSize Pixels = new(1600, 340);

    /// <summary>
    /// Below the middle of the view and a comfortable way out. Text over the centre is text in
    /// the way of flying, and 1.4 m across was tried in a previous implementation and read as
    /// enormous — close to fifty degrees of view.
    /// </summary>
    private static readonly SurfacePlacement Placed = new()
    {
        Lock = SurfaceLock.HeadLocked,
        DistanceMetres = 1.6f,
        DropMetres = -0.45f,
        PitchDegrees = 0f,
        WidthMetres = 0.9f,

        // Never curved. Two short lines in the middle of the view have no far edges to bring
        // closer, so a curved caption is a caption bent for no reason.
        Curvature = 0f,
        Opacity = 1f,
    };

    private readonly CaptionLayer _layer;
    private readonly CaptionViewModel _model = new();
    private readonly CaptionView _view;
    private readonly OffscreenSurface _offscreen;

    private bool _dirty = true;

    public VrCaptionSurface(CaptionLayer layer)
    {
        _layer = layer;
        _view = new CaptionView { DataContext = _model };
        _offscreen = new OffscreenSurface(_view, Pixels);

        _model.PropertyChanged += OnModelChanged;
        layer.Changed += OnLayerChanged;

        OnLayerChanged();
    }

    public bool Enabled { get; set; }

    public VrSurface Surface => VrSurface.Captions;

    /// <summary>
    /// Hidden when there is nothing to say. An empty caption quad is a transparent rectangle
    /// the compositor still composites, and one fewer visible overlay is one fewer thing
    /// between the Commander and the cockpit.
    /// </summary>
    public bool Visible => Enabled && _layer.Visible;

    public SurfacePlacement Placement => Placed;

    public (int Width, int Height) Size => (Pixels.Width, Pixels.Height);

    public bool IsDirty => _dirty;

    public void Observe(VrPose head)
    {
    }

    public void Draw(IntPtr destination, int rowBytes)
    {
        _offscreen.Render();
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;
    }

    /// <summary>Called when the caption settings change, so the size and box follow.</summary>
    public void Configure(CaptionSettings settings)
    {
        _layer.Settings = settings;
        _model.Configure(settings);
    }

    public void Dispose()
    {
        _layer.Changed -= OnLayerChanged;
        _model.PropertyChanged -= OnModelChanged;
        _offscreen.Dispose();
    }

    private void OnLayerChanged()
    {
        _model.Show(_layer.Lines);
        _dirty = true;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
}
