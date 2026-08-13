using System.Numerics;
using D47.Core.Vr;
using Valve.VR;

namespace D47.Vr;

/// <summary>
/// The one place a transform crosses between OpenVR and <see cref="Matrix4x4"/>.
/// <para>
/// <b>The rotation transposes on the crossing, and it is not a typo.</b>
/// <see cref="Matrix4x4"/> is row-vector — a point multiplies on the left and the translation
/// sits in the last row. OpenVR is column-vector: its matrix goes on the left and the
/// translation is the last column. So a basis vector that is a row here is a column there.
/// </para>
/// <para>
/// Both directions live next to each other deliberately, because a transposition mistake in
/// either is invisible for as long as everything is square to the view — which is exactly how
/// long it takes to convince yourself it works. Get it backwards and every unturned overlay
/// looks perfect while every turned one is mirrored, so the test that matters rotates on all
/// three axes.
/// </para>
/// </summary>
public static class VrMatrix
{
    public static HmdMatrix34_t ToOpenVr(VrPose pose) => ToOpenVr(pose.ToMatrix());

    public static HmdMatrix34_t ToOpenVr(Matrix4x4 transform) => new()
    {
        m0 = transform.M11, m1 = transform.M21, m2 = transform.M31, m3 = transform.M41,
        m4 = transform.M12, m5 = transform.M22, m6 = transform.M32, m7 = transform.M42,
        m8 = transform.M13, m9 = transform.M23, m10 = transform.M33, m11 = transform.M43,
    };

    public static Matrix4x4 ToMatrix(HmdMatrix34_t tracked) => new(
        tracked.m0, tracked.m4, tracked.m8, 0,
        tracked.m1, tracked.m5, tracked.m9, 0,
        tracked.m2, tracked.m6, tracked.m10, 0,
        tracked.m3, tracked.m7, tracked.m11, 1);

    public static VrPose ToPose(HmdMatrix34_t tracked) => VrPose.FromMatrix(ToMatrix(tracked));

    /// <summary>
    /// A tracked device's pose, or null if the runtime is not actually reporting one.
    /// <para>
    /// <b>Both flags, not either.</b> An all-zero device slot converts to a perfectly valid
    /// rotation: <see cref="Quaternion.CreateFromRotationMatrix"/> picks the largest diagonal,
    /// finds the square root of one, and hands back a unit quaternion — so an unchecked empty
    /// slot reads as a real controller sitting at the origin facing backwards. There is no
    /// later layer that catches this, and missing it mid-drag drags a grabbed panel to the
    /// Commander's feet the moment a controller goes to sleep.
    /// </para>
    /// </summary>
    public static VrPose? Real(TrackedDevicePose_t pose)
    {
        if (!pose.bPoseIsValid || !pose.bDeviceIsConnected)
        {
            return null;
        }

        var read = ToPose(pose.mDeviceToAbsoluteTracking);
        return read.IsFinite ? read : null;
    }
}
