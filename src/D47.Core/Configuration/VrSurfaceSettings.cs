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
/// are four properties of one quad (list.md Phase 9).
/// </summary>
public sealed record VrSurfaceSettings
{
    /// <summary>"head" or "world".</summary>
    public string Lock { get; init; } = "head";

    /// <summary>Metres in front of the anchor.</summary>
    public double Distance { get; init; } = 1.1;

    /// <summary>Metres below eye level. Negative is down.</summary>
    public double Drop { get; init; } = -0.25;

    /// <summary>Degrees tilted back towards the Commander, so a dropped panel still faces them.</summary>
    public double Pitch { get; init; } = 12;

    /// <summary>How wide the quad is, in metres. Height follows from the texture's aspect.</summary>
    public double Width { get; init; } = 1.1;

    /// <summary>
    /// 0 is flat and 1 is fully wrapped around the Commander. <em>Panels can switch between
    /// curved and flat</em> is this number reaching zero rather than a second mode — a mode
    /// would be a thing that can disagree with the number.
    /// </summary>
    public double Curvature { get; init; }

    public double Opacity { get; init; } = 0.95;

    /// <summary>
    /// The content's own scale, on the same ladder the desktop window zooms with. Distinct from
    /// mini mode, which reduces what is on the panel rather than how large it is drawn.
    /// </summary>
    public int Zoom { get; init; } = Interface.ZoomLadder.Default;

    public SurfacePlacement ToPlacement() => new SurfacePlacement
    {
        Lock = string.Equals(Lock, "world", StringComparison.OrdinalIgnoreCase)
            ? SurfaceLock.WorldLocked
            : SurfaceLock.HeadLocked,
        DistanceMetres = (float)Distance,
        DropMetres = (float)Drop,
        PitchDegrees = (float)Pitch,
        WidthMetres = (float)Width,
        Curvature = (float)Curvature,
        Opacity = (float)Opacity,
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
