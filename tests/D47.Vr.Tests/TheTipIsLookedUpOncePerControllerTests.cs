using System.Numerics;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The grip-to-tip correction is a property of the controller model, not of the frame, so it is
/// asked for once per device and not sixty times a second.
/// </summary>
public class TheTipIsLookedUpOncePerControllerTests
{
    [Fact]
    public void TheRenderModelIsAskedOncePerDeviceHoweverManyFramesGoBy()
    {
        var openVr = new FakeOpenVr();

        openVr.Poses[OpenVR.k_unTrackedDeviceIndex_Hmd] = FakeOpenVr.Tracked(Matrix4x4.Identity);
        openVr.Poses[3] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(0.2f, -0.3f, -0.4f));
        openVr.Poses[4] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(-0.2f, -0.3f, -0.4f));
        openVr.Classes[3] = ETrackedDeviceClass.Controller;
        openVr.Classes[4] = ETrackedDeviceClass.Controller;

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr) { Pointing = true };

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            for (var frame = 0; frame < 20; frame++)
            {
                Assert.Equal(2, runtime.HandsAndHead().Hands.Count);
            }

            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.GetComponentState)));
            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.RenderModelHasComponent)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>And what it answers is what the Commander aims with.</summary>
    [Fact]
    public void TheAimComesFromTheTipRatherThanTheGrip()
    {
        var openVr = new FakeOpenVr { GripToTip = Matrix4x4.CreateTranslation(0f, 0f, -0.07f) };

        openVr.Poses[OpenVR.k_unTrackedDeviceIndex_Hmd] = FakeOpenVr.Tracked(Matrix4x4.Identity);
        openVr.Poses[3] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(0.2f, -0.3f, -0.4f));
        openVr.Classes[3] = ETrackedDeviceClass.Controller;

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr) { Pointing = true };

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            var hand = Assert.Single(runtime.HandsAndHead().Hands);

            Assert.Equal(new Vector3(0.2f, -0.3f, -0.4f), hand.Grip.Position);
            Assert.Equal(new Vector3(0.2f, -0.3f, -0.47f), hand.Aim.Position);
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>A model with no tip component aims from the grip, and is not asked twice about it.</summary>
    [Fact]
    public void AModelWithNoTipAimsFromTheGrip()
    {
        var openVr = new FakeOpenVr();
        openVr.Components.Clear();

        openVr.Poses[OpenVR.k_unTrackedDeviceIndex_Hmd] = FakeOpenVr.Tracked(Matrix4x4.Identity);
        openVr.Poses[3] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(0.2f, -0.3f, -0.4f));
        openVr.Classes[3] = ETrackedDeviceClass.Controller;

        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr) { Pointing = true };

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            for (var frame = 0; frame < 5; frame++)
            {
                var hand = Assert.Single(runtime.HandsAndHead().Hands);
                Assert.Equal(hand.Grip.Position, hand.Aim.Position);
            }

            // Both component names tried, once, and never again.
            Assert.Equal(2, openVr.Count(nameof(FakeOpenVr.RenderModelHasComponent)));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.GetComponentState)));
        }
        finally
        {
            runtime.Stop();
        }
    }
}
