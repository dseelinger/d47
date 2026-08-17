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
    /// <para>
    /// The pitch is <see cref="EyeFacingPitch"/> plus <paramref name="pitchTrimRadians"/>, unless
    /// <paramref name="facesTheEyes"/> is false — which is the caption layer, and only it. See
    /// <see cref="EyeFacingPitch"/> for why the angle is derived rather than configured.
    /// </para>
    /// </summary>
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
    /// How far a surface has to tilt back to face the Commander's eyes, given how far ahead and
    /// how far below them it sits. Zero at eye level, and <em>negative</em> for a surface below
    /// one: an overlay's visible face looks along its own +Z, and a positive rotation about X
    /// carries that downwards. See <see cref="Resting"/>, which shipped with this sign inverted.
    /// <para>
    /// The same arithmetic <see cref="Resting"/> already does for a world-locked surface, for the
    /// same reason: a quad below eye level that is not tilted up is read edge-on. Head-locked took
    /// a fixed angle from settings instead, hand-tuned to 12° — within a degree of right for the
    /// full panel at 1.1 m and 0.25 m down, and six degrees short for mini at 0.9 m and 0.3 m
    /// down, where the geometry asks for 18.4°. That is the tell that it was a constant standing
    /// in for a derivation: it can only be correct for one distance and drop at a time, and there
    /// are two surfaces with two of each, both settable.
    /// </para>
    /// <para>
    /// Derived, so the tilt follows the distance and drop rows instead of going stale behind them.
    /// What is left of the setting is a trim on top of this, which is the part a Commander might
    /// actually have an opinion about.
    /// </para>
    /// </summary>
    public static float EyeFacingPitch(float distanceMetres, float dropMetres) =>
        MathF.Atan2(dropMetres, distanceMetres);

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

    /// <summary>
    /// Where a world-locked surface goes the first time it is shown, if nobody has ever put it
    /// anywhere (docs/plans/change-requests.md item 9).
    /// <para>
    /// <b>A world-locked surface with no placement rides the head</b>, so defaulting the lock to
    /// "world" and stopping there looks exactly like the change not having been made. Something
    /// has to choose a first position, and this is it.
    /// </para>
    /// <para>
    /// <paramref name="topEdgeMetres"/> is measured from the <em>floor</em>, because that is what
    /// "knee level" is measured from — the standing universe puts y=0 there. The centre is half a
    /// quad-height below it, which is the whole reason the caller has to know how tall the quad
    /// is: <see cref="SurfacePlacement.WidthMetres"/> is settable and the height is not, so the
    /// drop that puts the <em>top</em> anywhere depends on the texture's aspect and changes the
    /// moment the width does.
    /// </para>
    /// <para>
    /// Yaw only, and then pitched to face the Commander's eyes. A Commander who happens to be
    /// looking down when the panel first appears meant "in front of me", not "below where I was
    /// already looking" — and a quad near the floor that is not tilted up is a line.
    /// </para>
    /// </summary>
    public static VrPose Resting(
        VrPose head,
        float distanceMetres,
        float topEdgeMetres,
        float quadHeightMetres)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, head.Facing);
        var flattened = new Vector3(forward.X, 0f, forward.Z);

        // Straight up or straight down leaves no compass direction at all. Falling back to the
        // tracking universe's forward is arbitrary but finite, which the alternative is not.
        flattened = flattened.LengthSquared() < 1e-6f
            ? -Vector3.UnitZ
            : Vector3.Normalize(flattened);

        var centre = new Vector3(
            head.Position.X + (flattened.X * distanceMetres),
            topEdgeMetres - (quadHeightMetres / 2f),
            head.Position.Z + (flattened.Z * distanceMetres));

        // Tilted back by however far the eyes are above it, so the face of the quad points at
        // them rather than at the floor. At eye level this is zero and the panel is upright.
        //
        // NEGATIVE, and that is the whole of a bug this shipped with. An overlay's visible face
        // looks along its own +Z — it has to, or a head-locked panel at pitch zero would be
        // showing the Commander its back — and a positive rotation about X carries +Z downwards.
        // So the obvious sign tilts a panel at knee height to face the floor, through exactly
        // twice the angle it should have gone the other way. It is invisible in an assertion on
        // the angle, which is the right size either way, and only shows up in one on the
        // direction the face ends up pointing.
        var rise = head.Position.Y - centre.Y;
        var pitch = -MathF.Atan2(rise, distanceMetres);

        var yaw = MathF.Atan2(flattened.X, -flattened.Z);

        return new VrPose(centre, Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f));
    }

    /// <summary>
    /// About where a Commander's knees are, worked out from how high their eyes are.
    /// <para>
    /// Derived rather than assumed, because a fixed figure is wrong for anybody who is not the
    /// height it was picked for — and the floor is the one reference the runtime actually
    /// supplies. Knee height is close to 0.285 of stature across adult populations, and stature
    /// runs about 0.10 m above eye height.
    /// </para>
    /// <para>
    /// <b>Seated and standing are not told apart, and cannot be from this number alone.</b> A
    /// seated Commander's eyes are lower while their knees are not, so this reads low for them —
    /// which puts the panel further out of the way rather than in front of anything, and out of
    /// the way is what the request actually asked for. Clamped at both ends so a dropped tracking
    /// frame or a mis-set floor cannot put the panel underground or at head height.
    /// </para>
    /// </summary>
    public static float KneeHeight(float eyeHeightMetres) =>
        Math.Clamp((eyeHeightMetres + 0.10f) * 0.285f, 0.25f, 0.75f);
}
