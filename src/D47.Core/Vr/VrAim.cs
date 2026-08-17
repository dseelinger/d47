using System.Numerics;

namespace D47.Core.Vr;

/// <summary>
/// Where the aim beam and the cursor go.
/// <para>
/// Both exist because SteamVR's own laser does not reach here. That laser only runs over SteamVR's
/// dashboard, so the moment controller input moved to <c>IVRInput</c> there was nothing on screen
/// saying where the Commander was pointing — see <c>VrActionInput</c> for why it had to move. A
/// panel you cannot see yourself pointing at is one you cannot grab.
/// </para>
/// </summary>
public static class VrAim
{
    /// <summary>
    /// The beam texture is a few pixels across and very tall. The quad's height in the room follows
    /// its width by the texture's aspect, so a long thin texture is what makes a long thin beam
    /// <em>without</em> putting a scale into the transform — every overlay transform here is rigid,
    /// and a scale in one would quietly break the inverse the hit test takes.
    /// </summary>
    public const int BeamPixelsWide = 4;

    public const int BeamPixelsTall = 2048;

    private const float BeamAspect = (float)BeamPixelsWide / BeamPixelsTall;

    /// <summary>
    /// How far past the panel's edge a ray still counts as approaching, and so still draws a beam.
    /// The beam answers "where is the panel and am I close", so it has to appear <em>before</em>
    /// the ray is on the panel — but an always-on laser in a cockpit is its own nuisance, so it is
    /// bounded rather than permanent.
    /// </summary>
    public const float BeamGuideScale = 3f;

    /// <summary>How long the beam is drawn when it is near the panel but not on it.</summary>
    public const float BeamMissLengthMetres = 1.5f;

    /// <summary>
    /// Pushed off the surface toward the viewer so the cursor cannot z-fight with the panel under
    /// it, and small enough to read as sitting on the glass rather than floating in front of it.
    /// </summary>
    public const float CursorLiftMetres = 0.004f;

    /// <summary>About a fingertip at arm's length.</summary>
    public const float CursorSizeMetres = 0.02f;

    /// <summary>
    /// The overlay width in metres that yields a beam <paramref name="lengthMetres"/> long. The
    /// width grows with the length — about 4 mm at 2 m — which is the honest price of keeping the
    /// transform rigid.
    /// </summary>
    public static float BeamWidthFor(float lengthMetres) =>
        MathF.Max(1e-4f, lengthMetres * BeamAspect);

    /// <summary>The world point a ray reaches at <paramref name="distanceMetres"/>.</summary>
    /// <remarks>
    /// One value feeds both the cursor's position and the beam's length, which is what makes them
    /// meet — whether or not the curvature model is right about where that point really is.
    /// </remarks>
    public static Vector3 PointAlong(VrPose ray, float distanceMetres)
    {
        var direction = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, ray.Facing));
        return ray.Position + (direction * distanceMetres);
    }

    /// <summary>
    /// Where to put the beam quad so it lies along the aim direction and faces the viewer.
    /// <para>
    /// <b>The billboard is not a nicety.</b> The beam is a flat quad, and a flat quad seen edge-on
    /// is invisible — which is precisely the angle a beam pointing away from you tends to be seen
    /// at. So its long axis is pinned to the aim, and it is then rolled about that axis until its
    /// face turns toward the head.
    /// </para>
    /// <para>
    /// Orthonormal by construction: the desired facing is projected off the long axis rather than
    /// assumed perpendicular to it, and the third axis is derived from the other two, so a head
    /// directly along the beam degenerates gracefully instead of producing a zero vector.
    /// </para>
    /// </summary>
    public static VrPose BeamAlong(VrPose aim, Vector3 headPosition, float lengthMetres)
    {
        var forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, aim.Facing));
        var centre = aim.Position + (forward * (lengthMetres / 2f));

        var toHead = headPosition - centre;
        var facing = toHead - (Vector3.Dot(toHead, forward) * forward);

        if (facing.Length() < 1e-6f)
        {
            // Looking straight down the beam. Any perpendicular will do; the quad is a dot anyway.
            facing = Vector3.Transform(Vector3.UnitY, aim.Facing);
        }

        facing = Vector3.Normalize(facing);

        var x = Vector3.Normalize(Vector3.Cross(forward, facing));
        var z = Vector3.Cross(x, forward);

        // The beam's long axis is its own Y, because the texture is tall rather than wide.
        return Basis(x, forward, z, centre);
    }

    /// <summary>
    /// A quad centred on a point, turned to face the head and lifted slightly toward it.
    /// <para>
    /// The lift is along the view direction rather than along the panel's normal, deliberately: the
    /// panel's real normal at that point is a function of a curvature the runtime will not describe,
    /// whereas "toward the eye" is always the direction that keeps the sprite in front.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Three axes and a position as a pose. Row-vector, matching <see cref="VrPose.ToMatrix"/>:
    /// each local axis is a <em>row</em>, which is the transposition every OpenVR crossing turns on.
    /// </summary>
    private static VrPose Basis(Vector3 x, Vector3 y, Vector3 z, Vector3 position) =>
        VrPose.FromMatrix(new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            position.X, position.Y, position.Z, 1));
}
