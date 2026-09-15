using System.Numerics;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>"Place the panel here": the arithmetic, and the phrase reaching it with no model (#161).</summary>
public class PlacingThePanelWhereYouLookTests
{
    private static readonly Vector3 Eyes = new(0.2f, 1.2f, 0.4f);

    private static VrPose Head(float yawDegrees = 0f, float pitchDegrees = 0f, float rollDegrees = 0f) => new(
        Eyes,
        Quaternion.CreateFromYawPitchRoll(
            yawDegrees * MathF.PI / 180f,
            pitchDegrees * MathF.PI / 180f,
            rollDegrees * MathF.PI / 180f));

    private static Vector3 Face(VrPose pose) => Vector3.Transform(Vector3.UnitZ, pose.Facing);

    private static Vector3 Forward(VrPose pose) => Vector3.Transform(-Vector3.UnitZ, pose.Facing);

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(
            Vector3.Distance(expected, actual) < 1e-4f,
            $"expected {expected}, got {actual}");
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0f, -30f)]
    [InlineData(0f, 25f)]
    [InlineData(90f, 0f)]
    [InlineData(-60f, -20f)]
    public void TheCentreLiesOnTheHeadsetsForwardLineAndTheFaceLooksBack(float yaw, float pitch)
    {
        var head = Head(yaw, pitch);

        var placed = VrPlacementMath.Gazed(head, 1.1f);

        AssertNear(Eyes + (Forward(head) * 1.1f), placed.Position);
        AssertNear(Vector3.Normalize(Eyes - placed.Position), Face(placed));
    }

    [Fact]
    public void LookingDownPlacesItLowerAndLookingUpHigher()
    {
        var level = VrPlacementMath.Gazed(Head(), 1f);
        var down = VrPlacementMath.Gazed(Head(pitchDegrees: -30f), 1f);
        var up = VrPlacementMath.Gazed(Head(pitchDegrees: 30f), 1f);

        Assert.Equal(Eyes.Y, level.Position.Y, 4);
        Assert.Equal(Eyes.Y - 0.5f, down.Position.Y, 4);
        Assert.Equal(Eyes.Y + 0.5f, up.Position.Y, 4);
    }

    [Fact]
    public void LookingToOneSidePlacesItOnThatSide()
    {
        // A positive yaw turns the head's -Z towards -X, the Commander's left.
        var placed = VrPlacementMath.Gazed(Head(yawDegrees: 90f), 1f);

        AssertNear(Eyes + new Vector3(-1f, 0f, 0f), placed.Position);
        AssertNear(Vector3.UnitX, Face(placed));
    }

    [Fact]
    public void AHeadTiltedOnItsSideStillPlacesAPanelWithNoRoll()
    {
        var placed = VrPlacementMath.Gazed(Head(yawDegrees: 30f, pitchDegrees: -15f, rollDegrees: 35f), 1f);

        var right = Vector3.Transform(Vector3.UnitX, placed.Facing);

        Assert.Equal(0f, right.Y, 4);
    }

    private sealed record Fixture(CapabilityRegistry Registry, KeywordRouter Router, List<int> Placed);

    private static Fixture Build(VrGazeOutcome outcome = VrGazeOutcome.Placed)
    {
        var placed = new List<int>();
        var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            VrCapability.Create(
                settings,
                new VrCapability.HeadsetSurface
                {
                    Report = () => (VrState.Active, null),
                    Nudge = (_, _) => VrNudgeOutcome.Moved,
                    PlaceWhereLooking = () =>
                    {
                        placed.Add(1);
                        return outcome;
                    },
                }),
        ]);

        return new Fixture(registry, new KeywordRouter(registry), placed);
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance)
    {
        var match = fixture.Router.MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal("place_headset_panel_here", match.ToolName);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("place the panel here")]
    [InlineData("put the panel here")]
    [InlineData("place the VR panel here")]
    [InlineData("panel here")]
    public async Task SayingItPlacesThePanelWithNoModelInThePath(string said)
    {
        var fixture = Build();

        var result = await Say(fixture, said);

        Assert.False(result.IsError);
        Assert.Single(fixture.Placed);
    }

    [Fact]
    public async Task WithNoHeadPoseItAnswersAsANudgeDoes()
    {
        var result = await Say(Build(VrGazeOutcome.NoHeadset), "place the panel here");

        Assert.Equal(VrNudges.Describe(VrNudge.Left, VrNudgeOutcome.NoHeadset), result.Content);
    }
}
