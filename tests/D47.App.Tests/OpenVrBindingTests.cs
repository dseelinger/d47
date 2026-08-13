using System.Numerics;
using D47.Core.Vr;
using D47.Vr;
using Valve.VR;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The vendored binding and the one conversion that crosses into it. None of this needs a
/// headset, SteamVR, or even the native library — which is the point: these are the failures
/// that are silent on the machine that has all three.
/// </summary>
public class OpenVrBindingTests
{
    /// <summary>
    /// <b>The test that matters, and it rotates on all three axes deliberately.</b>
    /// <para>
    /// OpenVR is column-vector and <see cref="Matrix4x4"/> is row-vector, so the rotation
    /// transposes on the crossing. Get it backwards and every overlay square to the view looks
    /// perfect while every turned one is mirrored — which is exactly how long it takes to
    /// convince yourself the conversion works. A test at the identity pose proves nothing at
    /// all, and neither does one rotated about a single axis.
    /// </para>
    /// <para>
    /// The comparison applies the point through OpenVR's matrix by hand, in OpenVR's own
    /// convention, rather than through any of our own code. Both sides being ours is how a
    /// transposition test passes against a transposed conversion.
    /// </para>
    /// </summary>
    [Fact]
    public void ATransformSendsAPointToTheSamePlaceInBothConventions()
    {
        var pose = new VrPose(
            new Vector3(1.5f, 1.7f, -3.0f),
            Quaternion.CreateFromYawPitchRoll(0.7f, -0.2f, 0.1f));

        var handed = VrMatrix.ToOpenVr(pose);

        foreach (var point in Points())
        {
            var throughOpenVr = ApplyAsOpenVrWould(handed, point);
            var throughOurs = Vector3.Transform(point, pose.ToMatrix());

            Assert.Equal(throughOurs.X, throughOpenVr.X, 4);
            Assert.Equal(throughOurs.Y, throughOpenVr.Y, 4);
            Assert.Equal(throughOurs.Z, throughOpenVr.Z, 4);
        }
    }

    [Fact]
    public void ATransformSurvivesTheRoundTrip()
    {
        var pose = new VrPose(
            new Vector3(-0.4f, 1.2f, 0.9f),
            Quaternion.CreateFromYawPitchRoll(-1.1f, 0.35f, 0.8f));

        var back = VrMatrix.ToPose(VrMatrix.ToOpenVr(pose));

        Assert.Equal(pose.Position.X, back.Position.X, 4);
        Assert.Equal(pose.Position.Y, back.Position.Y, 4);
        Assert.Equal(pose.Position.Z, back.Position.Z, 4);

        // Compared by where they send a point rather than component-wise: q and -q are the
        // same rotation, and a component comparison fails on a conversion that is correct.
        foreach (var point in Points())
        {
            var expected = Vector3.Transform(point, pose.Facing);
            var actual = Vector3.Transform(point, back.Facing);

            Assert.Equal(expected.X, actual.X, 4);
            Assert.Equal(expected.Y, actual.Y, 4);
            Assert.Equal(expected.Z, actual.Z, 4);
        }
    }

    /// <summary>
    /// An all-zero device slot converts to a perfectly valid-looking pose:
    /// <see cref="Quaternion.CreateFromRotationMatrix"/> picks the largest diagonal, finds the
    /// square root of one, and hands back a unit quaternion. So an unchecked empty slot reads
    /// as a real controller sitting at the origin facing backwards, and there is no later layer
    /// that catches it — missing this mid-drag drags a grabbed panel to the Commander's feet
    /// the moment a controller goes to sleep.
    /// </summary>
    [Fact]
    public void AnEmptyDeviceSlotLooksPerfectlyValidWhichIsTheTrap()
    {
        var empty = default(TrackedDevicePose_t);

        var naive = VrMatrix.ToPose(empty.mDeviceToAbsoluteTracking);
        Assert.Equal(1f, naive.Facing.Length(), 5);
        Assert.True(naive.IsFinite);

        // Which is why the flags are checked, and why both of them are.
        Assert.Null(VrMatrix.Real(empty));
        Assert.Null(VrMatrix.Real(empty with { bPoseIsValid = true }));
        Assert.Null(VrMatrix.Real(empty with { bDeviceIsConnected = true }));
    }

    [Fact]
    public void ARealDeviceSlotComesThrough()
    {
        var pose = new VrPose(new Vector3(0.3f, 1.1f, -0.6f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f));

        var slot = new TrackedDevicePose_t
        {
            bPoseIsValid = true,
            bDeviceIsConnected = true,
            mDeviceToAbsoluteTracking = VrMatrix.ToOpenVr(pose),
        };

        var read = VrMatrix.Real(slot);

        Assert.NotNull(read);
        Assert.Equal(pose.Position.X, read.Value.Position.X, 4);
        Assert.Equal(pose.Position.Z, read.Value.Position.Z, 4);
    }

    /// <summary>
    /// The interface versions the vendored file was generated against. A bumped version with a
    /// stale struct is silent memory corruption rather than a compile error — the function
    /// table is an eighty-slot array of pointers and a wrong slot calls the wrong function with
    /// no error at all. When a refresh from ValveSoftware/openvr goes wrong, this is the first
    /// thing to look at.
    /// </summary>
    [Fact]
    public void TheVendoredBindingIsPinnedToTheInterfaceVersionsItWasGeneratedFor()
    {
        Assert.Equal("IVROverlay_028", OpenVR.IVROverlay_Version);
        Assert.Equal("IVRSystem_026", OpenVR.IVRSystem_Version);
    }

    /// <summary>
    /// Answering "is there a runtime here" must never throw, whatever this machine has. It is
    /// asked on every machine d47 runs on, including every one that has never had SteamVR.
    /// </summary>
    [Fact]
    public void LookingForTheRuntimeIsSafeOnAMachineThatHasNone()
    {
        var runtime = OpenVrLoader.RuntimePath();
        var dll = OpenVrLoader.Locate();

        if (runtime is not null)
        {
            Assert.True(Directory.Exists(runtime));
        }

        if (dll is not null)
        {
            Assert.True(File.Exists(dll));
        }

        // A located DLL implies a located runtime; the reverse is not implied, because a
        // runtime directory can exist with the library missing.
        Assert.True(dll is null || runtime is not null);
    }

    private static IReadOnlyList<Vector3> Points() =>
    [
        Vector3.Zero,
        Vector3.UnitX,
        Vector3.UnitY,
        Vector3.UnitZ,
        new(0.3f, -0.7f, 1.9f),
    ];

    /// <summary>
    /// OpenVR's own convention: the matrix goes on the left and the point is a column, so a row
    /// of the matrix dots the point and the translation is the fourth column.
    /// </summary>
    private static Vector3 ApplyAsOpenVrWould(HmdMatrix34_t where, Vector3 point) => new(
        (where.m0 * point.X) + (where.m1 * point.Y) + (where.m2 * point.Z) + where.m3,
        (where.m4 * point.X) + (where.m5 * point.Y) + (where.m6 * point.Z) + where.m7,
        (where.m8 * point.X) + (where.m9 * point.Y) + (where.m10 * point.Z) + where.m11);
}
