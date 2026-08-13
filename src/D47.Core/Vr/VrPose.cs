using System.Numerics;

namespace D47.Core.Vr;

/// <summary>
/// Where something is and which way it is facing, in the tracking universe. Metres,
/// right-handed, X right, Y up, Z <em>back</em> — so forward is -Z, which is OpenVR's
/// convention rather than ours and is the sign every placement bug starts with.
/// </summary>
public readonly record struct VrPose(Vector3 Position, Quaternion Orientation)
{
    public static readonly VrPose Origin = new(Vector3.Zero, Quaternion.Identity);

    /// <summary>
    /// The orientation, guaranteed unit length.
    /// <para>
    /// Normalised here rather than required of the caller, because a quaternion out of a
    /// tracking runtime drifts off unit length over a session, and one that is one per cent
    /// long scales everything it touches — so the panel creeps away from the Commander the
    /// longer they play.
    /// </para>
    /// </summary>
    public Quaternion Facing =>
        Orientation.LengthSquared() == 0 ? Quaternion.Identity : Quaternion.Normalize(Orientation);

    /// <summary>Whether every component is a real number. A dropped tracking frame is not.</summary>
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

/// <summary>
/// The arithmetic of putting a surface somewhere and keeping it there. Pure, and in Core on
/// purpose: if a headset is needed to check this, the maths is in the wrong place
/// (architecture.md §8).
/// </summary>
public static class VrPlacementMath
{
    /// <summary>
    /// Where a head-locked surface sits, given where the head is.
    /// <para>
    /// Read right to left: pitch the surface about its own centre, push it forward along the
    /// head's own -Z, then carry the result wherever the head is. Composed the other way round
    /// it still moves when the head moves — so every "it followed" assertion passes — and the
    /// distance comes out wrong, which is the part a Commander would actually notice.
    /// </para>
    /// </summary>
    public static VrPose HeadLocked(VrPose head, float distanceMetres, float dropMetres, float pitchRadians)
    {
        var offset = Matrix4x4.CreateTranslation(new Vector3(0, dropMetres, -distanceMetres));
        var pitch = Matrix4x4.CreateRotationX(pitchRadians);

        return VrPose.FromMatrix(pitch * offset * head.ToMatrix());
    }

    /// <summary>
    /// The offset to freeze when a surface is grabbed: <c>hand⁻¹ · surface</c>.
    /// </summary>
    public static Matrix4x4 Grab(VrPose hand, VrPose surface)
    {
        Matrix4x4.Invert(hand.ToMatrix(), out var inverse);
        return surface.ToMatrix() * inverse;
    }

    /// <summary>
    /// Where a grabbed surface is now: the frozen offset, reapplied to where the hand is.
    /// <para>
    /// Always measured from the grab origin rather than accumulated from the last answer.
    /// Accumulating makes the surface's speed a function of how often this is asked, which
    /// reads as the tracking being broken rather than as the arithmetic being wrong.
    /// </para>
    /// <para>
    /// Nothing re-faces the surface at the Commander while it is held. That was tried in a
    /// previous implementation and it is wrong: a panel forced upright and square cannot be
    /// tilted to read from below or turned to sit at an angle, which is most of what moving one
    /// is for.
    /// </para>
    /// </summary>
    public static VrPose Carried(Matrix4x4 grabbedOffset, VrPose hand) =>
        VrPose.FromMatrix(grabbedOffset * hand.ToMatrix());

    /// <summary>
    /// Moves a world-locked surface by the change in head pose since it was anchored
    /// (list.md Phase 9, "Re-anchor the panels").
    /// <para>
    /// Elite's in-game recenter moves the cockpit without telling SteamVR, so a world-locked
    /// surface drifts out of position with no event to hook. The delta is applied to every
    /// world-locked surface as a group, which is what preserves their relative layout instead
    /// of stacking them all in front of the Commander — the thing a naive "put it back where
    /// it started" does, and the reason this takes a whole set rather than one pose.
    /// </para>
    /// </summary>
    public static VrPose Reanchored(VrPose surface, VrPose anchoredAt, VrPose headNow)
    {
        // Yaw only. A Commander who leans or looks down while re-anchoring did not mean to
        // tip every panel by the same amount and hang them over their knees; what they meant
        // was "put these back in front of me". Height and roll come from where the surfaces
        // were placed, which is a choice they already made.
        var turn = Matrix4x4.CreateRotationY(YawOf(headNow) - YawOf(anchoredAt));

        var pivot = Matrix4x4.CreateTranslation(-anchoredAt.Position)
                    * turn
                    * Matrix4x4.CreateTranslation(headNow.Position);

        return VrPose.FromMatrix(surface.ToMatrix() * pivot);
    }

    /// <summary>
    /// The compass direction a pose is facing, in radians. Taken from where the pose sends its
    /// own forward rather than from the quaternion's components, so it is the same number
    /// whichever of the two equivalent quaternions arrived.
    /// </summary>
    public static float YawOf(VrPose pose)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, pose.Facing);
        return MathF.Atan2(-forward.X, -forward.Z);
    }
}
