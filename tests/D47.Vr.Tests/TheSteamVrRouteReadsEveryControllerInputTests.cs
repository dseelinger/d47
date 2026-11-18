using System.Numerics;
using D47.Core.Storage;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Valve.VR;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// On the SteamVR route every button and the stick reach <see cref="VrHand"/> per hand, whenever pointing
/// is on, and the controllers are claimed only while a ray is on the panel.
/// </summary>
[Trait("Category", "Integration")]
public class TheSteamVrRouteReadsEveryControllerInputTests
{
    private const string Left = "/user/hand/left";
    private const string Right = "/user/hand/right";

    private static string ActionOf(VrButton button) => button switch
    {
        VrButton.Trigger => VrActionManifest.GrabAction,
        VrButton.Grip => VrActionManifest.BackAction,
        VrButton.Primary => VrActionManifest.PrimaryAction,
        VrButton.Secondary => VrActionManifest.SecondaryAction,
        VrButton.Menu => VrActionManifest.MenuAction,
        _ => VrActionManifest.StickClickAction,
    };

    [Theory]
    [InlineData(VrButton.Trigger)]
    [InlineData(VrButton.Grip)]
    [InlineData(VrButton.Primary)]
    [InlineData(VrButton.Secondary)]
    [InlineData(VrButton.Menu)]
    [InlineData(VrButton.StickClick)]
    public void AButtonReachesItsBitOnTheHandThatHoldsIt(VrButton button)
    {
        using var session = Session();

        session.OpenVr.HeldOn.Add((ActionOf(button), Left));
        session.Input.Refresh();

        Assert.Equal(VrHand.Bit(button), session.Input.ReadHand(VrActionInput.LeftHand).Held);
        Assert.Equal(0u, session.Input.ReadHand(VrActionInput.RightHand).Held);

        session.OpenVr.HeldOn.Clear();
        session.OpenVr.HeldOn.Add((ActionOf(button), Right));

        Assert.Equal(0u, session.Input.ReadHand(VrActionInput.LeftHand).Held);
        Assert.Equal(VrHand.Bit(button), session.Input.ReadHand(VrActionInput.RightHand).Held);
    }

    [Fact]
    public void TheStickReachesTheHandThatMovesIt()
    {
        using var session = Session();

        session.OpenVr.Sticks[Right] = (0.5f, -1f);

        Assert.Equal(Vector2.Zero, session.Input.ReadHand(VrActionInput.LeftHand).Stick);
        Assert.Equal(new Vector2(0.5f, -1f), session.Input.ReadHand(VrActionInput.RightHand).Stick);
    }

    [Fact]
    public void AnInactiveActionReadsAsNotHeldAndAtRest()
    {
        using var session = Session();

        foreach (var button in Enum.GetValues<VrButton>())
        {
            session.OpenVr.HeldOn.Add((ActionOf(button), Left));
            session.OpenVr.Inactive.Add(ActionOf(button));
        }

        session.OpenVr.Sticks[Left] = (1f, 1f);
        session.OpenVr.Inactive.Add(VrActionManifest.StickAction);

        Assert.Equal((0u, Vector2.Zero), session.Input.ReadHand(VrActionInput.LeftHand));
    }

    [Fact]
    public void WithNoRayOnThePanelTheSetIsUpdatedAtPriorityZeroAndNothingIsClaimed()
    {
        using var session = Session();

        session.OpenVr.Calls.Clear();
        session.Input.TriggerHeld(false);
        session.Input.Refresh();

        var updates = session.OpenVr.Calls.Where(call => call.Function == nameof(FakeOpenVr.UpdateActionState)).ToList();

        Assert.NotEmpty(updates);
        Assert.All(updates, call => Assert.Equal(0ul, call.Subject));
        Assert.False(session.Input.HoldingPriority);
        Assert.DoesNotContain(session.Lines, line => line.Contains("Claimed", StringComparison.Ordinal));
    }

    [Fact]
    public void ARayOnThePanelClaimsAndARefreshKeepsTheClaim()
    {
        using var session = Session();

        session.Input.TriggerHeld(true);
        session.OpenVr.Calls.Clear();
        session.Input.Refresh();

        var update = Assert.Single(session.OpenVr.Calls, call => call.Function == nameof(FakeOpenVr.UpdateActionState));

        Assert.Equal((ulong)OpenVR.k_nActionSetOverlayGlobalPriorityMin, update.Subject);
        Assert.True(session.Input.HoldingPriority);
        Assert.Single(session.Lines, line => line.Contains("Claimed", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRuntimeFillsEachHandFromTheHandItsDeviceIs()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-actions-" + Guid.NewGuid().ToString("N"));
        var openVr = new FakeOpenVr();

        openVr.Poses[OpenVR.k_unTrackedDeviceIndex_Hmd] = FakeOpenVr.Tracked(Matrix4x4.Identity);
        openVr.Poses[3] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(-0.2f, -0.3f, -0.4f));
        openVr.Poses[4] = FakeOpenVr.Tracked(Matrix4x4.CreateTranslation(0.2f, -0.3f, -0.4f));
        openVr.Classes[3] = ETrackedDeviceClass.Controller;
        openVr.Classes[4] = ETrackedDeviceClass.Controller;
        openVr.Roles[3] = ETrackedControllerRole.LeftHand;
        openVr.Roles[4] = ETrackedControllerRole.RightHand;

        var runtime = new SteamVrRuntime(
            [],
            NullLogger<SteamVrRuntime>.Instance,
            openVr,
            files: new DiskFileSystem(),
            actionFolder: folder);

        try
        {
            Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
            runtime.PrepareInput();
            runtime.Pointing = true;

            openVr.HeldOn.Add((VrActionManifest.PrimaryAction, Left));
            openVr.HeldOn.Add((VrActionManifest.MenuAction, Right));
            openVr.Sticks[Right] = (0f, 1f);

            var hands = runtime.HandsAndHead().Hands;

            var left = Assert.Single(hands, hand => hand.Device == 3);
            var right = Assert.Single(hands, hand => hand.Device == 4);

            Assert.Equal(VrHand.Bit(VrButton.Primary), left.Held);
            Assert.Equal(VrHand.Bit(VrButton.Menu), right.Held);
            Assert.Equal(Vector2.Zero, left.Stick);
            Assert.Equal(new Vector2(0f, 1f), right.Stick);
            Assert.False(runtime.Actions.HoldingPriority);
        }
        finally
        {
            runtime.Stop();

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    private static InputSession Session()
    {
        var openVr = new FakeOpenVr();
        var error = EVRInitError.None;

        openVr.Init(ref error, EVRApplicationType.VRApplication_Overlay);

        var lines = new List<string>();
        var folder = Path.Combine(Path.GetTempPath(), "d47-actions-" + Guid.NewGuid().ToString("N"));
        var input = new VrActionInput(new RecordingLogger<VrActionInput>(lines), openVr);

        input.Register(new DiskFileSystem(), folder);

        return new InputSession(openVr, input, lines, folder);
    }

    private sealed class InputSession(FakeOpenVr openVr, VrActionInput input, List<string> lines, string folder)
        : IDisposable
    {
        public FakeOpenVr OpenVr { get; } = openVr;

        public VrActionInput Input { get; } = input;

        public List<string> Lines { get; } = lines;

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
