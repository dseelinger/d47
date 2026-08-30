using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// Moving a panel that is already down in the room, without a controller
/// (<a href="https://github.com/dseelinger/d47/issues/199">#199</a>).
/// <para>
/// All arithmetic, and in Core for the reason the rest of the placement maths is: a headset
/// needed to check this would mean it was in the wrong place. What no test here can say is
/// whether a step <em>feels</em> like the right size, and none of them pretend to.
/// </para>
/// <para>
/// The fixture throughout is a panel in front of a Commander at the origin looking along -Z,
/// facing them — which is the pose <c>Resting</c> produces and the one every assertion about
/// "left" and "nearer" has to be read against.
/// </para>
/// </summary>
public class NudgingAPlacedPanelTests
{
    /// <summary>
    /// A metre ahead of a Commander at the origin, with its face — the surface's own +Z — turned
    /// back at them. Upright unless a tilt is asked for.
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

        // And nothing else moved. A nudge that also changed the height or the distance would be
        // one gesture doing two things, which is the failure the axes are flattened to avoid.
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

    /// <summary>
    /// <b>The one a derivation off the panel's own face would get wrong.</b> A panel below eye
    /// level is tilted back to be read, so its face points partly upwards; moving along that face
    /// would raise the panel every time the Commander asked for it to come closer.
    /// </summary>
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

    /// <summary>
    /// Turning swings the face, and left means the Commander's left. The sign is the whole of
    /// this assertion: a rotation about +Y carries the face towards +X, which is their
    /// <em>right</em>, so the obvious sign turns the panel the wrong way and no assertion on the
    /// angle alone would see it.
    /// </summary>
    [Fact]
    public void TurningLeftSwingsTheFaceTowardsTheCommandersLeft()
    {
        var left = VrNudges.Apply(Ahead(), VrNudge.TurnLeft, 1);
        var right = VrNudges.Apply(Ahead(), VrNudge.TurnRight, 1);

        Assert.True(Face(left).X < -0.01f, $"turn-left sent the face to X={Face(left).X}");
        Assert.True(Face(right).X > 0.01f, $"turn-right sent the face to X={Face(right).X}");

        // In place. Turning is about the surface's own centre, not an orbit around the Commander.
        Assert.Equal(Ahead().Position, left.Position);
    }

    /// <summary>
    /// Tilting up lifts the face, and this is the second sign that reads backwards: a positive
    /// rotation about X carries +Z <em>downwards</em>, which is the bug <c>Resting</c> shipped
    /// with. An assertion on the angle is the right size either way; only one on where the face
    /// ends up pointing can tell them apart.
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

    /// <summary>
    /// <b>A nudge has to survive a re-anchor</b>, or the two most useful things a Commander can
    /// do to a panel by voice undo each other. Re-anchoring replays <c>Placed</c> against
    /// <c>PlacedAgainst</c>, so a nudge that also brings the second half up to the head now is
    /// left alone by a re-anchor from that same place — which is what the Commander means by
    /// having just said "there".
    /// </summary>
    [Fact]
    public void ANudgeSurvivesAReanchorFromWhereItWasMade()
    {
        var head = new VrPose(new Vector3(0, 1.65f, 0), Quaternion.Identity);

        var nudged = VrNudges.Apply(Ahead(), VrNudge.Left, 3);
        var reanchored = VrPlacementMath.Reanchored(nudged, head, head);

        Assert.Equal(nudged.Position.X, reanchored.Position.X, 4);
        Assert.Equal(nudged.Position.Y, reanchored.Position.Y, 4);
        Assert.Equal(nudged.Position.Z, reanchored.Position.Z, 4);
    }

    /// <summary>And it is carried, not discarded, when the Commander has since turned.</summary>
    [Fact]
    public void ANudgeIsCarriedThroughAReanchorAfterTurning()
    {
        var placed = new VrPose(new Vector3(0, 1.65f, 0), Quaternion.Identity);
        var turned = new VrPose(placed.Position, Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f, 0, 0));

        var straight = VrPlacementMath.Reanchored(Ahead(), placed, turned);
        var nudged = VrPlacementMath.Reanchored(VrNudges.Apply(Ahead(), VrNudge.Up, 4), placed, turned);

        Assert.Equal(straight.Position.Y + (4 * VrNudges.StepMetres), nudged.Position.Y, 4);
    }

    /// <summary>
    /// The wire vocabulary and the enum are one list. Two of them would be a tool advertising a
    /// value its own parser refuses, which reads as the model hallucinating.
    /// </summary>
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
    /// leaking out. A default arm that swallowed a new direction would say "tilted it down" about
    /// something else entirely.
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
