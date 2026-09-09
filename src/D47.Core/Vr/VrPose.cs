using System.Numerics;

namespace D47.Core.Vr;

/// <summary>Where something is and which way it is facing, in the tracking universe.</summary>
public readonly record struct VrPose(Vector3 Position, Quaternion Orientation)
{
    public static readonly VrPose Origin = new(Vector3.Zero, Quaternion.Identity);

    /// <summary>The orientation, guaranteed unit length.</summary>
    public Quaternion Facing =>
        Orientation.LengthSquared() == 0 ? Quaternion.Identity : Quaternion.Normalize(Orientation);

    /// <summary>Whether every component is a real number.</summary>
    public bool IsFinite =>
        Finite(Position.X) && Finite(Position.Y) && Finite(Position.Z)
        && Finite(Orientation.X) && Finite(Orientation.Y) && Finite(Orientation.Z) && Finite(Orientation.W);

    public Matrix4x4 ToMatrix()
    {
        var matrix = Matrix4x4.CreateFromQuaternion(Facing);
        matrix.Translation = Position;
        return matrix;
    }

    public static VrPose FromMatrix(Matrix4x4 matrix) =>
        new(matrix.Translation, Quaternion.CreateFromRotationMatrix(matrix));

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>The arithmetic of putting a surface somewhere and keeping it there.</summary>
public static class VrPlacementMath
{
    /// <summary>Where a head-locked surface sits, given where the head is.</summary>
    public static VrPose HeadLocked(
        VrPose head,
        float distanceMetres,
        float dropMetres,
        float pitchTrimRadians,
        bool facesTheEyes = true)
    {
        var offset = Matrix4x4.CreateTranslation(new Vector3(0, dropMetres, -distanceMetres));

        var angle = facesTheEyes
            ? EyeFacingPitch(distanceMetres, dropMetres) + pitchTrimRadians
            : pitchTrimRadians;

        var pitch = Matrix4x4.CreateRotationX(angle);

        return VrPose.FromMatrix(pitch * offset * head.ToMatrix());
    }

    /// <summary>
    /// How far a surface has to tilt back to face the Commander's eyes, given how far ahead and how far
    /// below them it sits.
    /// </summary>
    public static float EyeFacingPitch(float distanceMetres, float dropMetres) =>
        MathF.Atan2(dropMetres, distanceMetres);

    /// <summary>The offset to freeze when a surface is grabbed: <c>hand⁻¹ · surface</c>.</summary>
    public static Matrix4x4 Grab(VrPose hand, VrPose surface)
    {
        Matrix4x4.Invert(hand.ToMatrix(), out var inverse);
        return surface.ToMatrix() * inverse;
    }

    /// <summary>Where a grabbed surface is now: the frozen offset, reapplied to where the hand is.</summary>
    public static VrPose Carried(Matrix4x4 grabbedOffset, VrPose hand) =>
        VrPose.FromMatrix(grabbedOffset * hand.ToMatrix());

    /// <summary>The compass direction a pose is facing, in radians.</summary>
    public static float YawOf(VrPose pose)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, pose.Facing);
        return MathF.Atan2(-forward.X, -forward.Z);
    }

    /// <summary>
    /// The same pose with its roll taken out: same place, same yaw, same pitch, level with the horizon
    /// (#189).
    /// </summary>
    public static VrPose Upright(VrPose pose)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, pose.Facing);
        var horizontal = new Vector3(forward.X, 0f, forward.Z);

        if (horizontal.LengthSquared() < 1e-6f)
        {
            var up = Vector3.Transform(Vector3.UnitY, pose.Facing);

            horizontal = new Vector3(up.X, 0f, up.Z) * (forward.Y > 0f ? -1f : 1f);
        }

        // Both vertical at once is a pose with no orientation left to preserve, which a real tracking frame
        // cannot be.
        if (horizontal.LengthSquared() < 1e-6f)
        {
            return pose with { Orientation = Quaternion.Identity };
        }

        var yaw = MathF.Atan2(-horizontal.X, -horizontal.Z);
        var pitch = MathF.Asin(Math.Clamp(forward.Y, -1f, 1f));

        return pose with { Orientation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f) };
    }

    /// <summary>
    /// Where a world-locked surface goes the first time it is shown, if nobody has ever put it anywhere
    /// (docs/plans/change-requests.md item 9).
    /// </summary>
    public static VrPose Resting(
        VrPose head,
        float distanceMetres,
        float topEdgeMetres,
        float quadHeightMetres)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, head.Facing);
        var flattened = new Vector3(forward.X, 0f, forward.Z);

        // Straight up or straight down leaves no compass direction at all.
        flattened = flattened.LengthSquared() < 1e-6f
            ? -Vector3.UnitZ
            : Vector3.Normalize(flattened);

        var centre = new Vector3(
            head.Position.X + (flattened.X * distanceMetres),
            topEdgeMetres - (quadHeightMetres / 2f),
            head.Position.Z + (flattened.Z * distanceMetres));

        // Tilted back by however far the eyes are above it, so the face of the quad points at them rather
        // than at the floor.
        var rise = head.Position.Y - centre.Y;
        var pitch = -MathF.Atan2(rise, distanceMetres);

        var yaw = MathF.Atan2(flattened.X, -flattened.Z);

        return new VrPose(centre, Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f));
    }

    /// <summary>About where a Commander's knees are, worked out from how high their eyes are.</summary>
    public static float KneeHeight(float eyeHeightMetres) =>
        Math.Clamp((eyeHeightMetres + 0.10f) * 0.285f, 0.25f, 0.75f);
}
