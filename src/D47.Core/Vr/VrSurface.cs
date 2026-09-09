using System.Numerics;

namespace D47.Core.Vr;

/// <summary>Which surface a placement belongs to.</summary>
public enum VrSurface
{
    PanelFull,
    PanelMini,

    /// <summary>Flat, output only, and not reachable from Overlay Positioning.</summary>
    Captions,
}

/// <summary>Head-locked or world-locked, per surface (Phase 9, "VR Panel locking").</summary>
public enum SurfaceLock
{
    /// <summary>Carried by the head.</summary>
    HeadLocked,

    /// <summary>Put down in the world and left there.</summary>
    WorldLocked,
}

/// <summary>Everything Overlay Positioning &amp; Look configures, for one surface.</summary>
public sealed record SurfacePlacement
{
    public SurfaceLock Lock { get; init; } = SurfaceLock.HeadLocked;

    /// <summary>
    /// How far in front. 1.4 m was tried in a previous implementation and read as enormous — close to
    /// fifty degrees of view, so the panel filled the middle and the cockpit was behind it rather than
    /// around it.
    /// </summary>
    public float DistanceMetres { get; init; } = 1.1f;

    /// <summary>How far below eye level, in metres.</summary>
    public float DropMetres { get; init; } = -0.25f;

    /// <summary>A trim on top of the tilt that already faces the Commander, in degrees.</summary>
    public float PitchDegrees { get; init; }

    /// <summary>
    /// Whether the surface tilts to face the Commander's eyes, or holds whatever <see
    /// cref="PitchDegrees"/> says outright.
    /// </summary>
    public bool FacesTheEyes { get; init; } = true;

    public float WidthMetres { get; init; } = 1.0f;

    /// <summary>0 is flat, 1 is fully wrapped.</summary>
    public float Curvature { get; init; }

    public float Opacity { get; init; } = 0.95f;

    /// <summary>The content scale, on the same ladder the desktop window zooms with.</summary>
    public int ZoomPercent { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>
    /// Where it was put down, for a world-locked surface, and where the head was when it was put there.
    /// </summary>
    public VrPose? Placed { get; init; }

    public VrPose? PlacedAgainst { get; init; }

    /// <summary>Clamped to what SteamVR and a human will actually accept.</summary>
    public SurfacePlacement Sane() => this with
    {
        DistanceMetres = Math.Clamp(DistanceMetres, 0.3f, 5f),
        DropMetres = Math.Clamp(DropMetres, -2f, 2f),
        PitchDegrees = Math.Clamp(PitchDegrees, -60f, 60f),
        WidthMetres = Math.Clamp(WidthMetres, 0.15f, 4f),
        Curvature = Math.Clamp(Curvature, 0f, 1f),
        Opacity = Math.Clamp(Opacity, 0.1f, 1f),
        ZoomPercent = Interface.ZoomLadder.Snap(ZoomPercent),
    };

    /// <summary>Where this surface goes, given where the head is now.</summary>
    public bool RidesTheHead => Lock != SurfaceLock.WorldLocked || Placed is null;

    /// <summary>
    /// Where this surface sits relative to the head itself, for a runtime that can hang an overlay off
    /// the headset rather than being told a room position every frame.
    /// </summary>
    public VrPose AgainstTheHead(VrPose head)
    {
        if (!Matrix4x4.Invert(head.ToMatrix(), out var inverse))
        {
            // A head pose that cannot be inverted is not a pose.
            return Where(VrPose.Origin);
        }

        return VrPose.FromMatrix(Where(head).ToMatrix() * inverse);
    }

    /// <summary>The offset against a head at the origin, which is one that is already level.</summary>
    public VrPose AgainstTheHead() => AgainstTheHead(VrPose.Origin);

    /// <summary>Where this surface goes, given where the head is now.</summary>
    public VrPose Where(VrPose head)
    {
        if (Lock == SurfaceLock.WorldLocked && Placed is { } placed)
        {
            return placed;
        }

        return VrPlacementMath.HeadLocked(
            VrPlacementMath.Upright(head),
            DistanceMetres,
            DropMetres,
            PitchDegrees * MathF.PI / 180f,
            FacesTheEyes);
    }
}
