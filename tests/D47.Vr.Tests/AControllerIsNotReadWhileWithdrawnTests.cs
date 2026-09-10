using System.Numerics;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// With the motion-controller row off, d47 does not touch the controllers at all — not their poses,
/// not their class, not their activity level (#198).
/// </summary>
public class AControllerIsNotReadWhileWithdrawnTests
{
    [Fact]
    public void WithdrawnMeansTheHeadIsStillReadAndNothingElseIs()
    {
        var openVr = Room();
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            openVr.Calls.Clear();

            var (hands, head) = runtime.HandsAndHead();

            Assert.Empty(hands);
            Assert.NotNull(head);

            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.GetTrackedDeviceClass)));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.GetTrackedDeviceActivityLevel)));
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.RenderModelHasComponent)));

            // The head comes from a one-device read, not from the whole universe.
            Assert.Equal(1, openVr.Count(nameof(FakeOpenVr.GetDeviceToAbsoluteTrackingPose), 1));
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>And the row going on is what puts the controllers back within reach.</summary>
    [Fact]
    public void TheRowGoingOnIsWhatPutsThemBackWithinReach()
    {
        var openVr = Room();
        var runtime = new SteamVrRuntime([], NullLogger<SteamVrRuntime>.Instance, openVr);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);

            runtime.Pointing = true;
            var (hands, _) = runtime.HandsAndHead();

            var hand = Assert.Single(hands);
            Assert.Equal(3u, hand.Device);
            Assert.True(openVr.Count(nameof(FakeOpenVr.GetTrackedDeviceClass)) > 0);

            runtime.Pointing = false;
            openVr.Calls.Clear();

            Assert.Empty(runtime.HandsAndHead().Hands);
            Assert.Equal(0, openVr.Count(nameof(FakeOpenVr.GetTrackedDeviceClass)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    /// <summary>A headset and one controller, both reporting.</summary>
    private static FakeOpenVr Room()
    {
        var openVr = new FakeOpenVr();

        openVr.Poses[OpenVR.k_unTrackedDeviceIndex_Hmd] = FakeOpenVr.Tracked(Matrix4x4.Identity);
        openVr.Poses[3] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(0.2f, -0.3f, -0.4f));
        openVr.Classes[3] = ETrackedDeviceClass.Controller;

        return openVr;
    }
}
