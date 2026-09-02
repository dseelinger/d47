using System.ComponentModel;
using Avalonia;
using D47.App.Panel;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The caption quad (Phase 9, "TheApp appears in the headset").
/// <para>
/// Its own overlay handle, flat, and unmovable. Not a preference — a caption the Commander can
/// drag is a caption they can drag somewhere they will not see it, and the one thing a caption
/// has to be is where they are already looking. Being a separate handle is also what keeps
/// <em>Overlay Positioning &amp; Look</em> from reaching it by accident.
/// </para>
/// <para>
/// <b>Two computed positions, and a row that picks between them</b>
/// (<a href="https://github.com/dseelinger/d47/issues/204">#204</a>). Unmovable survives that:
/// both poses are worked out here from the geometry, neither is placed, and no distance, curve
/// or grab reaches either. What changed is that head-locked stopped being the only answer, on
/// the Commander's argument that a band bolted to the view is shaky and a motion-sickness
/// source — see <see cref="CaptionSettings.Lock"/>.
/// </para>
/// </summary>
public sealed class VrCaptionSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>
    /// Wide and short: <see cref="Caption.WindowLines"/> lines of forty-two characters and
    /// nothing else. The height carries the box's padding as well as the text, so it is not
    /// simply two times a line — and it is unchanged from when the window held three, which is
    /// room the box's own padding takes up rather than a stale number.
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

        // Square to the view, not tilted at the eye. Captions are two or three short lines read
        // at a glance in the middle of the picture; the panel derives its tilt because it is
        // furniture you look down at, and a caption is not.
        FacesTheEyes = false,
        WidthMetres = 0.9f,

        // Never curved. Two short lines in the middle of the view have no far edges to bring
        // closer, so a curved caption is a caption bent for no reason.
        Curvature = 0f,
        Opacity = 1f,
    };

    /// <summary>
    /// How far ahead of the seated Commander the world-locked band sits, and how far below their
    /// eyes its centre goes (#204). Between the console and the feet: the centre lands 40° below
    /// the eyeline and the strip runs 37° to 43°, where the head-locked band runs 12° to 19°.
    /// </summary>
    private const float FootwellDistanceMetres = 0.80f;

    private const float FootwellDropMetres = -0.67f;

    /// <summary>
    /// The world-locked band, worked out once from the geometry above.
    /// <para>
    /// <b>Placed against the seated origin rather than against a head pose, and that is the whole
    /// of why nothing here needs re-anchoring.</b> Every absolute overlay d47 places goes into
    /// <c>TrackingUniverseSeated</c>, whose zero <em>is</em> the Commander's seated eye facing
    /// their forward — so "between the console and the feet" is a constant in that universe, and
    /// SteamVR's own <em>Reset Seated Position</em> carries the band with it. A pose frozen off a
    /// head sample instead would keep whatever lean was in that one frame, and would need a way
    /// back that no longer exists: re-anchor was retired in 0.94.0 (#219).
    /// </para>
    /// <para>
    /// <b>The width follows the distance, so the size row means the same thing in both modes.</b>
    /// Apparent text size is the texture's pixel count and the quad's width in metres together,
    /// and the band is now 1.04 m from the eye where it was 1.66 m — so holding 0.9 m would draw
    /// every caption 59% larger. Scaled by the ratio of the two eye distances, the band subtends
    /// the same 30.3° it always has and the three <see cref="CaptionSize"/> steps carry over
    /// unchanged rather than needing re-measuring.
    /// </para>
    /// <para>
    /// The tilt comes from <see cref="VrPlacementMath.Resting"/>, which aims the quad's face at
    /// the eye and pins roll to zero. The tilt is the one that matters: a band 40° below the eye
    /// that is square to the tracking universe is read edge-on. Roll costs nothing here and buys
    /// a little — head-locked has been levelled since #189 shipped in 0.93.0, but it is levelled
    /// once a serve while the runtime carries the quad rigidly at headset rate in between, so a
    /// quick roll of the head tilts it until the next frame. A band the headset is not carrying
    /// has nothing to correct.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Hidden when there is nothing to say. An empty caption quad is a transparent rectangle
    /// the compositor still composites, and one fewer visible overlay is one fewer thing
    /// between the Commander and the cockpit.
    /// </summary>
    public bool Visible => Enabled && _layer.Visible;

    /// <summary>
    /// Captions are read, never touched. An interactive quad in front of the cockpit is a laser
    /// stopping on a label and a hand that cannot reach past it.
    /// </summary>
    public bool TakesPointer => false;

    /// <summary>
    /// Which of the two bands is up. Read from the layer's own settings on every serve rather
    /// than latched at configure time, so the row and the quad cannot come to disagree — the
    /// failure the panel's lock logs a warning about. Both answers are constants worked out
    /// above; neither is a position anything put anywhere.
    /// </summary>
    public SurfacePlacement Placement =>
        _layer.Settings.Locking == SurfaceLock.WorldLocked ? Footwell : Placed;

    public (int Width, int Height) Size => (Pixels.Width, Pixels.Height);

    public bool IsDirty => _dirty;

    /// <summary>
    /// Nothing to remember. The head-locked band is carried by the runtime and the world-locked
    /// one is a constant in the seated universe, so neither answer depends on where the head was
    /// on any particular frame — which is what keeps the world-locked band out of the "computed
    /// from one lean and stuck with it" trap (#204).
    /// </summary>
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
    /// A world-locked band the given distance ahead of the seated eye, with its centre the given
    /// drop below it, and as wide as it has to be to look the size the head-locked one does.
    /// </summary>
    private static SurfacePlacement Below(float distanceMetres, float dropMetres)
    {
        var inTheView = Placed;

        // Eye to quad centre, for each band. The slant rather than the distance ahead: at 40°
        // down the drop is most of the triangle, and measuring along the floor alone would draw
        // the band a fifth too small.
        var wasAway = MathF.Sqrt(
            (inTheView.DistanceMetres * inTheView.DistanceMetres)
            + (inTheView.DropMetres * inTheView.DropMetres));

        var nowAway = MathF.Sqrt((distanceMetres * distanceMetres) + (dropMetres * dropMetres));

        var width = inTheView.WidthMetres * nowAway / wasAway;

        // The quad's height is not settable — SteamVR takes a width and derives the rest off the
        // texture's aspect — so the top edge that puts the centre at the asked-for drop has to
        // come from both, and moves the moment either of them does.
        var quadHeight = width * Pixels.Height / Pixels.Width;

        return inTheView with
        {
            Lock = SurfaceLock.WorldLocked,
            DistanceMetres = distanceMetres,
            DropMetres = dropMetres,
            WidthMetres = width,

            // The seated origin standing in for the head, which is what it is: the Commander's
            // eye, facing their forward, level. Resting does the rest — the yaw off that
            // forward, the tilt back at the eye, and the roll pinned to zero.
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
