using System.ComponentModel;
using Avalonia;
using D47.App.Panel;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>The caption quad (Phase 9, "TheApp appears in the headset").</summary>
public sealed class VrCaptionSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>
    /// Wide and short: <see cref="Caption.WindowLines"/> lines of forty-two characters and nothing
    /// else.
    /// </summary>
    private static readonly PixelSize Pixels = new(1600, 340);

    /// <summary>Below the middle of the view and a comfortable way out.</summary>
    private static readonly SurfacePlacement Placed = new()
    {
        Lock = SurfaceLock.HeadLocked,
        DistanceMetres = 1.6f,
        DropMetres = -0.45f,
        PitchDegrees = 0f,

        // Square to the view, not tilted at the eye.
        FacesTheEyes = false,
        WidthMetres = 0.9f,

        // Never curved.
        Curvature = 0f,
        Opacity = 1f,
    };

    /// <summary>
    /// How far ahead of the seated Commander the world-locked band sits, and how far below their eyes
    /// its centre goes (#204).
    /// </summary>
    private const float FootwellDistanceMetres = 0.80f;

    private const float FootwellDropMetres = -0.67f;

    /// <summary>The world-locked band, worked out once from the geometry above.</summary>
    private static readonly SurfacePlacement Footwell = Below(
        FootwellDistanceMetres,
        FootwellDropMetres);

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

    /// <summary>Hidden when there is nothing to say.</summary>
    public bool Visible => Enabled && _layer.Visible;

    /// <summary>Captions are read, never touched.</summary>
    public bool TakesPointer => false;

    /// <summary>Which of the two bands is up.</summary>
    public SurfacePlacement Placement =>
        _layer.Settings.Locking == SurfaceLock.WorldLocked ? Footwell : Placed;

    public (int Width, int Height) Size => (Pixels.Width, Pixels.Height);

    public bool IsDirty => _dirty;

    /// <summary>Nothing to remember.</summary>
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

    /// <summary>
    /// A world-locked band the given distance ahead of the seated eye, with its centre the given drop
    /// below it, and as wide as it has to be to look the size the head-locked one does.
    /// </summary>
    private static SurfacePlacement Below(float distanceMetres, float dropMetres)
    {
        var inTheView = Placed;

        // Eye to quad centre, for each band.
        var wasAway = MathF.Sqrt(
            (inTheView.DistanceMetres * inTheView.DistanceMetres)
            + (inTheView.DropMetres * inTheView.DropMetres));

        var nowAway = MathF.Sqrt((distanceMetres * distanceMetres) + (dropMetres * dropMetres));

        var width = inTheView.WidthMetres * nowAway / wasAway;

        // The quad's height is not settable — SteamVR takes a width and derives the rest off the texture's
        // aspect — so the top edge that puts the centre at the asked-for drop has to come from both, and
        // moves the moment either of them does.
        var quadHeight = width * Pixels.Height / Pixels.Width;

        return inTheView with
        {
            Lock = SurfaceLock.WorldLocked,
            DistanceMetres = distanceMetres,
            DropMetres = dropMetres,
            WidthMetres = width,

            // The seated origin standing in for the head, which is what it is: the Commander's eye, facing
            // their forward, level.
            Placed = VrPlacementMath.Resting(
                VrPose.Origin,
                distanceMetres,
                dropMetres + (quadHeight / 2f),
                quadHeight),
        };
    }

    private void OnLayerChanged()
    {
        _model.Show(_layer.Lines);
        _dirty = true;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
}
