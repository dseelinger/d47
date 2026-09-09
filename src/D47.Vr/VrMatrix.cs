using System.Numerics;
using D47.Core.Vr;
using Valve.VR;

namespace D47.Vr;

/// <summary>The one place a transform crosses between OpenVR and <see cref="Matrix4x4"/>.</summary>
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

    /// <summary>A tracked device's pose, or null if the runtime is not actually reporting one.</summary>
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
