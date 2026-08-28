using System.Numerics;
using D47.Core.Vr;

namespace D47.Core.Configuration;

/// <summary>
/// A pose as a file holds it. Seven numbers, spelled out, rather than
/// <see cref="Vector3"/> and <see cref="Quaternion"/> straight into the serializer: the store
/// rejects unknown keys, so the shape of what is written has to be a shape this file declares
/// rather than one a framework happens to produce this version.
/// </summary>
public sealed record PoseSettings
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }

    public double QX { get; init; }

    public double QY { get; init; }

    public double QZ { get; init; }

    public double QW { get; init; } = 1;

    public static PoseSettings From(VrPose pose) => new()
    {
        X = pose.Position.X,
        Y = pose.Position.Y,
        Z = pose.Position.Z,
        QX = pose.Facing.X,
        QY = pose.Facing.Y,
        QZ = pose.Facing.Z,
        QW = pose.Facing.W,
    };

    public VrPose ToPose() => new(
        new Vector3((float)X, (float)Y, (float)Z),
        new Quaternion((float)QX, (float)QY, (float)QZ, (float)QW));
}

/// <summary>
/// Where one surface sits and what it looks like — <em>VR Panel locking</em>, <em>Overlay
/// Positioning &amp; Look</em>, <em>Panels can switch between curved and flat</em> and
/// <em>Scale the big panel</em>, which are four checklist items over one record because they
/// are four properties of one quad (Phase 9).
/// </summary>
public sealed record VrSurfaceSettings
{
    /// <summary>
    /// "head" or "world".
    /// <para>
    /// World out of the box (docs/plans/change-requests.md item 9). A panel that follows the
    /// Commander's gaze is in the way of whatever they turned to look at, which is the one thing
    /// a companion beside a flight sim must not be; put down in the room it is somewhere they
    /// glance at instead of something they see through.
    /// </para>
    /// <para>
    /// <b>This string on its own changes nothing.</b> A world-locked surface that has never been
    /// put anywhere still rides the head — see <c>VrSurface.RidesTheHead</c> — so the default
    /// only takes effect because a first position is computed on first show. The two go together
    /// and neither works alone.
    /// </para>
    /// </summary>
    public string Lock { get; init; } = "world";

    /// <summary>Metres in front of the anchor.</summary>
    public double Distance { get; init; } = 1.1;

    /// <summary>Metres below eye level. Negative is down.</summary>
    public double Drop { get; init; } = -0.25;

    /// <summary>Degrees tilted back towards the Commander, so a dropped panel still faces them.</summary>
    /// <summary>
    /// A trim on the tilt, in degrees, on top of the angle that already faces the Commander's
    /// eyes. Zero means exactly at them.
    /// <para>
    /// It used to be the whole angle and defaulted to 12, which could only be right for one
    /// distance and drop — see <see cref="Vr.VrPlacementMath.EyeFacingPitch"/>. Files written
    /// before that carry the 12 and are cleared once by the repair
    /// <see cref="VrSettings.PitchRepaired"/> counts.
    /// </para>
    /// </summary>
    public double Pitch { get; init; }

    /// <summary>How wide the quad is, in metres. Height follows from the texture's aspect.</summary>
    public double Width { get; init; } = 1.1;

    /// <summary>
    /// 0 is flat and 1 is fully wrapped around the Commander. <em>Panels can switch between
    /// curved and flat</em> is this number reaching zero rather than a second mode — a mode
    /// would be a thing that can disagree with the number.
    /// </summary>
    public double Curvature { get; init; }

    /// <summary>
    /// <b>Read by nothing since 0.60.7, and kept because the settings file is append-only.</b>
    /// How see-through the glass is turned out to be one preference rather than one per surface,
    /// so it moved to <see cref="VrSettings.Opacity"/> and this copy stays on disk holding whatever
    /// it last held. <c>SettingsStore</c> carries the value up once, under
    /// <see cref="VrSettings.OpacityShared"/>.
    /// </summary>
    public double Opacity { get; init; } = 0.95;

    /// <summary>
    /// The content's own scale, on the same ladder the desktop window zooms with. Distinct from
    /// mini mode, which reduces what is on the panel rather than how large it is drawn.
    /// </summary>
    public int Zoom { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>
    /// How many pixels this surface is rendered at, as "1280x800" (Phase 25, "The panel
    /// resizes and zooms").
    /// <para>
    /// The third of the three levers, and the one that was a constant until now: pixels decide
    /// how much the image can hold, <see cref="Width"/> decides how big it looks in the room, and
    /// <see cref="Zoom"/> decides how much logical layout those pixels carry. See
    /// <see cref="Interface.PanelResolution"/> for why every rung holds one aspect and why the
    /// ceiling is a judgement rather than a limit.
    /// </para>
    /// <para>
    /// A string rather than two integers because it is one choice from one row: two numbers that
    /// can be edited separately are two numbers that can disagree about the aspect, and the
    /// aspect is what keeps this lever independent of the one beside it. Empty means the default,
    /// which is what every file written before this property existed says — the settings file is
    /// append-only, so an older file has to mean something rather than fail.
    /// </para>
    /// </summary>
    public string Pixels { get; init; } = string.Empty;

    /// <summary>The rung <see cref="Pixels"/> names, snapped, with the default for anything else.</summary>
    public (int Width, int Height) Resolution => Interface.PanelResolution.Parse(
        string.IsNullOrWhiteSpace(Pixels) ? null : Pixels);

    /// <summary>
    /// Where this surface goes and what it looks like.
    /// <para>
    /// <b>The opacity is handed in, because it is not this surface's to keep</b> (asked for
    /// 2026-08-24). Both panels are as see-through as each other, so the number lives once on
    /// <see cref="VrSettings.Opacity"/> — see the comment there for why this one of the six is the
    /// odd one out. <see cref="Opacity"/> below is what the file used to hold and nothing reads.
    /// </para>
    /// </summary>
    public SurfacePlacement ToPlacement(double opacity) => new SurfacePlacement
    {
        Lock = string.Equals(Lock, "world", StringComparison.OrdinalIgnoreCase)
            ? SurfaceLock.WorldLocked
            : SurfaceLock.HeadLocked,
        DistanceMetres = (float)Distance,
        DropMetres = (float)Drop,
        PitchDegrees = (float)Pitch,
        WidthMetres = (float)Width,
        Curvature = (float)Curvature,
        Opacity = (float)opacity,
        ZoomPercent = Zoom,
    }.Sane();

    /// <summary>The default placement for the mini panel: smaller, nearer, further out of the way.</summary>
    public static VrSurfaceSettings Mini() => new()
    {
        Distance = 0.9,
        Drop = -0.30,
        Width = 0.34,
    };
}
