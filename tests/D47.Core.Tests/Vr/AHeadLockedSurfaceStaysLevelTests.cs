using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// A surface carried by the head follows where the Commander is looking and not how their head is
/// tilted.
/// </summary>
public class AHeadLockedSurfaceStaysLevelTests
{
    /// <summary>The caption quad's real placement, so the assertions are about what ships.</summary>
    private static readonly SurfacePlacement Captions = new()
    {
        Lock = SurfaceLock.HeadLocked,
        DistanceMetres = 1.6f,
        DropMetres = -0.45f,
        PitchDegrees = 0f,
        FacesTheEyes = false,
        WidthMetres = 0.9f,
    };

    private static VrPose Head(float yaw = 0f, float pitch = 0f, float roll = 0f) => new(
        new Vector3(0.2f, 1.65f, -0.4f),
        Quaternion.CreateFromYawPitchRoll(Radians(yaw), Radians(pitch), Radians(roll)));

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;

    /// <summary>How far a pose is rolled, measured off where its own lateral axis points.</summary>
    private static float RollOf(VrPose pose) =>
        Vector3.Transform(Vector3.UnitX, pose.Facing).Y;

    private static Vector3 Forward(VrPose pose) =>
        Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, pose.Facing));

    [Theory]
    [InlineData(0f)]
    [InlineData(3f)]
    [InlineData(-12f)]
    [InlineData(40f)]
    public void TheSurfaceIsLevelHoweverTheHeadIsTilted(float roll)
    {
        var placed = Captions.Where(Head(yaw: 25f, pitch: -18f, roll: roll));

        Assert.Equal(0f, RollOf(placed), 4);
    }

    /// <summary>And the roll is the only thing dropped.</summary>
    [Fact]
    public void ItStillFollowsWhereTheCommanderIsLooking()
    {
        var level = Captions.Where(Head(yaw: 25f, pitch: -18f));
        var tilted = Captions.Where(Head(yaw: 25f, pitch: -18f, roll: 15f));

        Assert.Equal(level.Position.X, tilted.Position.X, 4);
        Assert.Equal(level.Position.Y, tilted.Position.Y, 4);
        Assert.Equal(level.Position.Z, tilted.Position.Z, 4);

        // Ahead of the head and below it, which is where a caption goes.
        var head = Head(yaw: 25f, pitch: -18f);
        var toward = Vector3.Normalize(tilted.Position - head.Position);

        Assert.True(Vector3.Dot(toward, Forward(head)) > 0.9f);
    }

    /// <summary>The property that makes the two derivations one.</summary>
    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(25f, -18f, 15f)]
    [InlineData(-140f, 35f, -8f)]
    public void TheOffsetHandedToTheRuntimeLandsExactlyWhereWhereSaysItGoes(float yaw, float pitch, float roll)
    {
        var head = Head(yaw, pitch, roll);

        var composed = VrPose.FromMatrix(Captions.AgainstTheHead(head).ToMatrix() * head.ToMatrix());
        var wanted = Captions.Where(head);

        Assert.Equal(wanted.Position.X, composed.Position.X, 4);
        Assert.Equal(wanted.Position.Y, composed.Position.Y, 4);
        Assert.Equal(wanted.Position.Z, composed.Position.Z, 4);

        Assert.Equal(1f, MathF.Abs(Quaternion.Dot(wanted.Facing, composed.Facing)), 4);
    }

    /// <summary>
    /// A head that is already level asks for nothing, which is what keeps this from being a change to
    /// the shipped placement rather than a correction to it: the offset for an upright head is the pure
    /// translation it always was, and the quad is written once and left.
    /// </summary>
    [Fact]
    public void AnUprightHeadAsksForNoCorrectionAtAll()
    {
        // The offset for a head that is not tilted is the pure translation it has always been — no rotation
        // in it at all — whichever way that head is turned or pitched.
        foreach (var head in new[] { Head(), Head(yaw: 25f, pitch: -18f), Head(yaw: -140f, pitch: 35f) })
        {
            var offset = Captions.AgainstTheHead(head);

            Assert.Equal(1f, MathF.Abs(offset.Facing.W), 4);
            Assert.Equal(0f, offset.Position.X, 4);
            Assert.Equal(Captions.DropMetres, offset.Position.Y, 4);
            Assert.Equal(-Captions.DistanceMetres, offset.Position.Z, 4);
        }

        Assert.Equal(1f, MathF.Abs(Captions.AgainstTheHead().Facing.W), 4);
    }

    /// <summary>The correction is the head's roll, counter-turned, and nothing else.</summary>
    [Theory]
    [InlineData(10f)]
    [InlineData(-22f)]
    public void TheOffsetCarriesTheHeadsRollBackwards(float roll)
    {
        var offset = Captions.AgainstTheHead(Head(roll: roll));

        // Its lateral axis is tilted by the same angle the other way, which is what cancels when the runtime
        // composes it with the headset's own pose.
        Assert.Equal(-MathF.Sin(Radians(roll)), RollOf(offset), 3);
    }

    /// <summary>A surface put down in the room is untouched.</summary>
    [Fact]
    public void AWorldLockedSurfaceIsNotTouched()
    {
        var placed = new VrPose(
            new Vector3(0, 0.9f, -1.2f),
            Quaternion.CreateFromYawPitchRoll(0.2f, -0.3f, 0.4f));

        var surface = Captions with { Lock = SurfaceLock.WorldLocked, Placed = placed };

        Assert.Equal(placed, surface.Where(Head(roll: 30f)));
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(30f, 20f, 0f)]
    [InlineData(30f, 20f, 45f)]
    [InlineData(-95f, -60f, -170f)]
    public void UprightKeepsTheDirectionAndDropsTheTilt(float yaw, float pitch, float roll)
    {
        var pose = Head(yaw, pitch, roll);
        var upright = VrPlacementMath.Upright(pose);

        // The same way, to four places: roll turns a pose about its own forward, so forward is exactly what
        // survives being levelled.
        Assert.Equal(Forward(pose).X, Forward(upright).X, 4);
        Assert.Equal(Forward(pose).Y, Forward(upright).Y, 4);
        Assert.Equal(Forward(pose).Z, Forward(upright).Z, 4);

        Assert.Equal(0f, RollOf(upright), 4);
        Assert.Equal(pose.Position, upright.Position);
    }

    /// <summary>Straight down is the case that would have swung a caption across the cockpit.</summary>
    [Theory]
    [InlineData(90f)]
    [InlineData(-90f)]
    public void LookingStraightUpOrDownDoesNotSwingTheCaptionRound(float pitch)
    {
        foreach (var yaw in new[] { 0f, 90f, -140f })
        {
            // Continuity is the claim, because at exactly vertical there is no yaw left to assert: a yaw read
            // out of atan2(0, 0) is whatever the sign of a zero happens to be, and the failure it causes is a
            // caption that jumps to the other side of the cockpit for as long as the Commander looks at their
            // feet.
            var nearly = Captions.Where(Head(yaw: yaw, pitch: pitch - (0.1f * MathF.Sign(pitch))));
            var straight = Captions.Where(Head(yaw: yaw, pitch: pitch));

            Assert.True(
                Vector3.Distance(nearly.Position, straight.Position) < 0.02f,
                $"looking {pitch:0}° at a yaw of {yaw:0}° moved the caption "
                + $"{Vector3.Distance(nearly.Position, straight.Position):0.00} m");

            Assert.Equal(0f, RollOf(straight), 4);
        }
    }
}
