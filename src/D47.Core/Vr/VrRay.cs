using System.Numerics;

namespace D47.Core.Vr;

/// <summary>How big a quad is in the room: its width in metres, and the aspect of its texture.</summary>
/// <param name="WidthMetres">Settable. The height is not — it follows from the aspect.</param>
/// <param name="Aspect">Texture width over height.</param>
public readonly record struct VrExtent(float WidthMetres, float Aspect)
{
    public float HeightMetres => Aspect > 0 ? WidthMetres / Aspect : WidthMetres;
}

/// <summary>
/// Where a ray met a quad: across and down its face in 0..1, and how far along the ray that was.
/// </summary>
/// <param name="U">Across, from the left edge.</param>
/// <param name="V">Down, from the <em>top</em> edge — raster order, not OpenVR's Y-up.</param>
/// <param name="DistanceMetres">Along the ray, from where it started.</param>
public readonly record struct VrHit(float U, float V, float DistanceMetres);

/// <summary>
/// Where a controller's ray meets a panel, worked out here rather than asked of the runtime.
/// <para>
/// <b>Why not <c>ComputeOverlayIntersection</c>.</b> It answers only the flat case usefully: on a
/// curved overlay it was measured returning true for every ray with its surface coordinates stuck
/// at (0, 1), so it cannot say <em>where</em> on a curved panel a ray landed. Since the cursor has
/// to be placed at that point, and the beam has to stop there, one model that answers both is
/// worth more than a runtime call that answers neither — and it makes the whole thing testable
/// without a headset, which is the standing rule for anything in Core.
/// </para>
/// </summary>
public static class VrRay
{
    /// <summary>
    /// Below this a panel is treated as flat. Not an arbitrary epsilon: the curved path divides by
    /// the cylinder radius, which runs to infinity as curvature runs to zero, so flat is the limit
    /// rather than a special case — and the two agree to well under a pixel here.
    /// </summary>
    private const float Flat = 1e-3f;

    /// <summary>
    /// OpenVR does not document what <c>fCurvature</c> actually draws and no call will say. This is
    /// the working model: curvature 1 wraps the panel into a full circle, so it subtends
    /// curvature times tau of arc. Only where the cursor is drawn rests on it, which is what makes
    /// an undocumented constant acceptable here and nowhere else.
    /// </summary>
    private const float ArcPerCurvature = MathF.Tau;

    /// <summary>The radius of the cylinder a curved panel of this width lies on.</summary>
    public static float RadiusFor(float widthMetres, float curvature) =>
        widthMetres / (curvature * ArcPerCurvature);

    /// <summary>
    /// Where a ray in the tracking universe meets the panel, or null.
    /// <para>
    /// The panel lies in its own XY plane centred on its own origin, with its visible face toward
    /// +Z. Null covers a miss, a ray parallel to the surface, a ray pointing away, and a ray
    /// arriving at the <em>back</em> — returning the nearest edge instead would be a cursor stuck
    /// to the rim of a panel the Commander has stopped pointing at.
    /// </para>
    /// </summary>
    public static VrHit? Hit(VrPose surface, VrExtent extent, float curvature, VrPose ray)
    {
        var direction = Vector3.Transform(-Vector3.UnitZ, ray.Facing);
        return Hit(surface, extent, curvature, ray.Position, direction);
    }

    public static VrHit? Hit(
        VrPose surface,
        VrExtent extent,
        float curvature,
        Vector3 origin,
        Vector3 direction)
    {
        if (!Matrix4x4.Invert(surface.ToMatrix(), out var inverse))
        {
            return null;
        }

        var o = Vector3.Transform(origin, inverse);
        var d = Vector3.TransformNormal(direction, inverse);

        return curvature < Flat ? FlatHit(o, d, extent) : CurvedHit(o, d, extent, curvature);
    }

    /// <summary>Ray against the plane z = 0 in panel space, front face only.</summary>
    private static VrHit? FlatHit(Vector3 o, Vector3 d, VrExtent extent)
    {
        // Travelling toward +Z means arriving at the back, which is not a hit.
        if (d.Z >= -1e-9f)
        {
            return null;
        }

        var t = -o.Z / d.Z;

        return t <= 0f ? null : Land(o.X + (d.X * t), o.Y + (d.Y * t), t, extent);
    }

    /// <summary>
    /// Ray against a vertical cylinder section whose axis sits <c>radius</c> <em>behind</em> the
    /// panel centre, so the surface still passes through the origin — which is what makes the flat
    /// and curved paths agree exactly as curvature goes to zero.
    /// </summary>
    private static VrHit? CurvedHit(Vector3 o, Vector3 d, VrExtent extent, float curvature)
    {
        var r = RadiusFor(extent.WidthMetres, curvature);
        var cz = -r;

        // The axis is vertical, so Y drops out of the quadratic entirely.
        var ox = o.X;
        var oz = o.Z - cz;
        var a = (d.X * d.X) + (d.Z * d.Z);

        if (a < 1e-12f)
        {
            return null;
        }

        var b = 2f * ((ox * d.X) + (oz * d.Z));
        var c = (ox * ox) + (oz * oz) - (r * r);
        var discriminant = (b * b) - (4f * a * c);

        if (discriminant < 0f)
        {
            return null;
        }

        var root = MathF.Sqrt(discriminant);
        var near = (-b - root) / (2f * a);
        var far = (-b + root) / (2f * a);

        foreach (var t in new[] { MathF.Min(near, far), MathF.Max(near, far) })
        {
            if (t <= 0f)
            {
                continue;
            }

            var p = o + (d * t);

            // The outward normal points away from the axis, so a front-face arrival is one where
            // the ray travels against it.
            var nx = p.X;
            var nz = p.Z - cz;

            if ((d.X * nx) + (d.Z * nz) >= 0f)
            {
                continue;
            }

            // Arc length from the panel's centre line, signed. That is the flat x coordinate.
            if (Land(MathF.Atan2(p.X, nz) * r, p.Y, t, extent) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>Panel-space metres to a hit, or null when the point is off the surface.</summary>
    private static VrHit? Land(float x, float y, float t, VrExtent extent)
    {
        var halfWidth = extent.WidthMetres / 2f;
        var halfHeight = extent.HeightMetres / 2f;

        if (MathF.Abs(x) > halfWidth || MathF.Abs(y) > halfHeight)
        {
            return null;
        }

        return new VrHit(
            (x + halfWidth) / (2f * halfWidth),
            (halfHeight - y) / (2f * halfHeight),
            t);
    }

    /// <summary>
    /// Which hand is pointing at the panel and where its ray lands, nearest hit winning.
    /// <para>
    /// Cast from <see cref="VrHand.Aim"/> rather than the grip, which is the whole reason that
    /// property exists. Only one question needs the answer to be exact — where to draw the cursor —
    /// and telling one hand from the other is far easier than that, because the two hands are
    /// nowhere near each other.
    /// </para>
    /// </summary>
    public static (VrHand Hand, VrHit Hit)? PointingAt(
        IReadOnlyList<VrHand> hands,
        VrPose surface,
        VrExtent extent,
        float curvature)
    {
        (VrHand, VrHit)? nearest = null;
        var closest = float.MaxValue;

        foreach (var hand in hands)
        {
            if (Hit(surface, extent, curvature, hand.Aim) is { } hit && hit.DistanceMetres < closest)
            {
                closest = hit.DistanceMetres;
                nearest = (hand, hit);
            }
        }

        return nearest;
    }
}
