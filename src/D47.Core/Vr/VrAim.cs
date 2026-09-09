using System.Numerics;

namespace D47.Core.Vr;

/// <summary>Where the aim beam and the cursor go.</summary>
public static class VrAim
{
    /// <summary>The beam texture is a few pixels across and very tall.</summary>
    public const int BeamPixelsWide = 4;

    public const int BeamPixelsTall = 2048;

    private const float BeamAspect = (float)BeamPixelsWide / BeamPixelsTall;

    /// <summary>
    /// How far past the panel's edge a ray still counts as approaching, and so still draws a beam.
    /// </summary>
    public const float BeamGuideScale = 3f;

    /// <summary>How long the beam is drawn when it is near the panel but not on it.</summary>
    public const float BeamMissLengthMetres = 1.5f;

    /// <summary>
    /// Pushed off the surface toward the viewer so the cursor cannot z-fight with the panel under it,
    /// and small enough to read as sitting on the glass rather than floating in front of it.
    /// </summary>
    public const float CursorLiftMetres = 0.004f;

    /// <summary>About a fingertip at arm's length.</summary>
    public const float CursorSizeMetres = 0.02f;

    /// <summary>The overlay width in metres that yields a beam <paramref name="lengthMetres"/> long.</summary>
    public static float BeamWidthFor(float lengthMetres) =>
        MathF.Max(1e-4f, lengthMetres * BeamAspect);

    /// <summary>The world point a ray reaches at <paramref name="distanceMetres"/>.</summary>
    public static Vector3 PointAlong(VrPose ray, float distanceMetres)
    {
        var direction = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, ray.Facing));
        return ray.Position + (direction * distanceMetres);
    }

    /// <summary>Where to put the beam quad so it lies along the aim direction and faces the viewer.</summary>
    public static VrPose BeamAlong(VrPose aim, Vector3 headPosition, float lengthMetres)
    {
        var forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, aim.Facing));
        var centre = aim.Position + (forward * (lengthMetres / 2f));

        var toHead = headPosition - centre;
        var facing = toHead - (Vector3.Dot(toHead, forward) * forward);

        if (facing.Length() < 1e-6f)
        {
            // Looking straight down the beam.
            facing = Vector3.Transform(Vector3.UnitY, aim.Facing);
        }

        facing = Vector3.Normalize(facing);

        var x = Vector3.Normalize(Vector3.Cross(forward, facing));
        var z = Vector3.Cross(x, forward);

        // The beam's long axis is its own Y, because the texture is tall rather than wide.
        return Basis(x, forward, z, centre);
    }

    /// <summary>A quad centred on a point, turned to face the head and lifted slightly toward it.</summary>
    public static VrPose CursorAt(Vector3 point, Vector3 headPosition)
    {
        var toward = headPosition - point;

        toward = toward.Length() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(toward);

        var x = Vector3.Cross(Vector3.UnitY, toward);

        if (x.Length() < 1e-6f)
        {
            // Looking straight up or straight down the world Y axis, where "up" says nothing.
            x = Vector3.Cross(Vector3.UnitX, toward);
        }

        x = Vector3.Normalize(x);

        var y = Vector3.Cross(toward, x);

        // The visible face looks along +Z, so +Z is what points at the eye.
        return Basis(x, y, toward, point + (toward * CursorLiftMetres));
    }

    /// <summary>Three axes and a position as a pose.</summary>
    private static VrPose Basis(Vector3 x, Vector3 y, Vector3 z, Vector3 position) =>
        VrPose.FromMatrix(new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            position.X, position.Y, position.Z, 1));
}
