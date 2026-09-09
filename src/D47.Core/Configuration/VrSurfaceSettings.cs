using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Vr;

namespace D47.Core.Configuration;

/// <summary>A pose as a file holds it.</summary>
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
/// Where one surface sits and what it looks like — VR Panel locking, Overlay Positioning &amp; Look,
/// Panels can switch between curved and flat and Scale the big panel, which are four checklist items
/// over one record because they are four properties of one quad (Phase 9).
/// </summary>
public sealed record VrSurfaceSettings
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>"head" or "world".</summary>
    public string Lock { get; init; } = "world";

    /// <summary>Metres in front of the anchor.</summary>
    public double Distance { get; init; } = 1.1;

    /// <summary>Metres below eye level.</summary>
    public double Drop { get; init; } = -0.25;

    /// <summary>Degrees tilted back towards the Commander, so a dropped panel still faces them.</summary>
    public double Pitch { get; init; }

    /// <summary>How wide the quad is, in metres.</summary>
    public double Width { get; init; } = 1.1;

    /// <summary>0 is flat and 1 is fully wrapped around the Commander.</summary>
    public double Curvature { get; init; }

    /// <summary>Read by nothing since 0.60.7, and kept because the settings file is append-only.</summary>
    public double Opacity { get; init; } = 0.95;

    /// <summary>The content's own scale, on the same ladder the desktop window zooms with.</summary>
    public int Zoom { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>
    /// How many pixels this surface is rendered at, as "1280x800" (Phase 25, "The panel resizes and
    /// zooms").
    /// </summary>
    public string Pixels { get; init; } = string.Empty;

    /// <summary>The rung <see cref="Pixels"/> names, snapped, with the default for anything else.</summary>
    public (int Width, int Height) Resolution => Interface.PanelResolution.Parse(
        string.IsNullOrWhiteSpace(Pixels) ? null : Pixels);

    /// <summary>Where this surface goes and what it looks like.</summary>
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
