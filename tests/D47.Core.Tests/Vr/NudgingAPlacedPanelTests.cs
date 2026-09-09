using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Moving a panel that is already down in the room, without a controller.</summary>
public class NudgingAPlacedPanelTests
{
    /// <summary>
    /// A metre ahead of a Commander at the origin, with its face — the surface's own +Z — turned back
    /// at them.
    /// </summary>
    private static VrPose Ahead(float pitchDegrees = 0f) => new(
        new Vector3(0, 1.2f, -1f),
        Quaternion.CreateFromYawPitchRoll(0f, pitchDegrees * MathF.PI / 180f, 0f));

    /// <summary>Where the face of a surface points.</summary>
    private static Vector3 Face(VrPose pose) => Vector3.Transform(Vector3.UnitZ, pose.Facing);

    [Fact]
    public void RightIsTheCommandersRightAndLeftIsTheOtherWay()
    {
        var right = VrNudges.Apply(Ahead(), VrNudge.Right, 1);
        var left = VrNudges.Apply(Ahead(), VrNudge.Left, 1);

        Assert.Equal(VrNudges.StepMetres, right.Position.X, 4);
        Assert.Equal(-VrNudges.StepMetres, left.Position.X, 4);

        // And nothing else moved.
        Assert.Equal(Ahead().Position.Y, right.Position.Y, 4);
        Assert.Equal(Ahead().Position.Z, right.Position.Z, 4);
    }

    [Fact]
    public void UpAndDownAreTheRoomsVerticalAndNothingElse()
    {
        var up = VrNudges.Apply(Ahead(), VrNudge.Up, 2);

        Assert.Equal(Ahead().Position.Y + (2 * VrNudges.StepMetres), up.Position.Y, 4);
        Assert.Equal(Ahead().Position.X, up.Position.X, 4);
        Assert.Equal(Ahead().Position.Z, up.Position.Z, 4);
    }

    [Fact]
    public void NearerClosesTheGapAndFurtherOpensIt()
    {
        var nearer = VrNudges.Apply(Ahead(), VrNudge.Nearer, 1);
        var further = VrNudges.Apply(Ahead(), VrNudge.Further, 1);

        // The panel is at -Z and the Commander at the origin, so nearer is towards zero.
        Assert.Equal(-1f + VrNudges.StepMetres, nearer.Position.Z, 4);
        Assert.Equal(-1f - VrNudges.StepMetres, further.Position.Z, 4);
    }

    /// <summary>The one a derivation off the panel's own face would get wrong.</summary>
    [Fact]
    public void NearerRunsAlongTheFloorEvenWhenThePanelIsTilted()
    {
        var tilted = Ahead(pitchDegrees: -25f);
        var nearer = VrNudges.Apply(tilted, VrNudge.Nearer, 1);

        Assert.Equal(tilted.Position.Y, nearer.Position.Y, 4);
        Assert.Equal(-1f + VrNudges.StepMetres, nearer.Position.Z, 4);
    }

    /// <summary>And the same for left, which a rolled or tilted panel could otherwise smear.</summary>
    [Fact]
    public void LeftRunsAlongTheFloorEvenWhenThePanelIsTilted()
    {
        var tilted = Ahead(pitchDegrees: -25f);
        var left = VrNudges.Apply(tilted, VrNudge.Left, 1);

        Assert.Equal(tilted.Position.Y, left.Position.Y, 4);
        Assert.Equal(-VrNudges.StepMetres, left.Position.X, 4);
    }

    /// <summary>Turning swings the face, and left means the Commander's left.</summary>
    [Fact]
    public void TurningLeftSwingsTheFaceTowardsTheCommandersLeft()
    {
        var left = VrNudges.Apply(Ahead(), VrNudge.TurnLeft, 1);
        var right = VrNudges.Apply(Ahead(), VrNudge.TurnRight, 1);

        Assert.True(Face(left).X < -0.01f, $"turn-left sent the face to X={Face(left).X}");
        Assert.True(Face(right).X > 0.01f, $"turn-right sent the face to X={Face(right).X}");

        // In place.
        Assert.Equal(Ahead().Position, left.Position);
    }

    /// <summary>
    /// Tilting up lifts the face, and this is the second sign that reads backwards: a positive rotation
    /// about X carries +Z downwards, which is the bug <c>Resting</c> shipped with.
    /// </summary>
    [Fact]
    public void TiltingUpLiftsTheFaceAndTiltingDownDropsIt()
    {
        var up = VrNudges.Apply(Ahead(), VrNudge.TiltUp, 1);
        var down = VrNudges.Apply(Ahead(), VrNudge.TiltDown, 1);

        Assert.True(Face(up).Y > 0.01f, $"tilt-up sent the face to Y={Face(up).Y}");
        Assert.True(Face(down).Y < -0.01f, $"tilt-down sent the face to Y={Face(down).Y}");

        Assert.Equal(Ahead().Position, up.Position);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(500, VrNudges.MostSteps)]
    public void OneCallMovesAtLeastOneStepAndAtMostAScore(int asked, int taken)
    {
        Assert.Equal(taken, VrNudges.Steps(asked));

        var moved = VrNudges.Apply(Ahead(), VrNudge.Right, asked);

        Assert.Equal(taken * VrNudges.StepMetres, moved.Position.X, 4);
    }

    /// <summary>The wire vocabulary and the enum are one list.</summary>
    [Fact]
    public void EveryDirectionHasExactlyOneNameAndEveryNameParsesBackToIt()
    {
        var directions = Enum.GetValues<VrNudge>();

        Assert.Equal(directions.Length, VrNudges.Names.Count);
        Assert.Equal(VrNudges.Names.Count, VrNudges.Names.Distinct(StringComparer.Ordinal).Count());

        foreach (var direction in directions)
        {
            Assert.Equal(direction, VrNudges.Parse(VrNudges.Names[(int)direction]));

            // Case is what a spoken phrase and a model both get wrong first.
            Assert.Equal(direction, VrNudges.Parse(VrNudges.Names[(int)direction].ToUpperInvariant()));
        }

        Assert.Null(VrNudges.Parse("sideways"));
        Assert.Null(VrNudges.Parse(null));
        Assert.Null(VrNudges.Parse("  "));
    }

    /// <summary>
    /// Every direction and every outcome has a sentence, and none of them is the enum's own name
    /// leaking out.
    /// </summary>
    [Fact]
    public void EveryDirectionAndOutcomeIsSaidInWords()
    {
        foreach (var direction in Enum.GetValues<VrNudge>())
        {
            foreach (var outcome in Enum.GetValues<VrNudgeOutcome>())
            {
                var said = VrNudges.Describe(direction, outcome);

                Assert.False(string.IsNullOrWhiteSpace(said));
                Assert.EndsWith(".", said, StringComparison.Ordinal);
                Assert.DoesNotContain(direction.ToString(), said, StringComparison.Ordinal);
            }
        }

        Assert.Equal("Moved it left.", VrNudges.Describe(VrNudge.Left, VrNudgeOutcome.Moved));
        Assert.Contains("put it down", VrNudges.Describe(VrNudge.Left, VrNudgeOutcome.PutDown), StringComparison.Ordinal);
        Assert.Contains("no headset", VrNudges.Describe(VrNudge.Left, VrNudgeOutcome.NoHeadset), StringComparison.OrdinalIgnoreCase);
    }
}
