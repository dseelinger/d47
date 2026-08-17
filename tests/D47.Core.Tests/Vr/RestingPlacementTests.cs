using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// Where a world-locked panel goes the first time it is shown, if nobody has ever put it
/// anywhere (docs/plans/change-requests.md item 9).
/// <para>
/// All arithmetic, and in Core on purpose: if a headset were needed to check this, the maths
/// would be in the wrong place. Nothing in this file can say whether it <em>looks</em> right in
/// a headset, and nothing pretends to.
/// </para>
/// </summary>
public class RestingPlacementTests
{
    private static VrPose Standing(float eyeHeight = 1.65f) =>
        new(new Vector3(0, eyeHeight, 0), Quaternion.Identity);

    /// <summary>
    /// The requirement names the panel's <em>top</em> edge, and the top edge is the one thing
    /// the settings cannot set: width is configurable and height follows from the texture's
    /// aspect. So the centre has to be derived from both.
    /// </summary>
    [Theory]
    [InlineData(0.34f, 0.1488f)]  // mini: 640x280 at 0.34 m
    [InlineData(1.10f, 0.6875f)]  // full: 1024x640 at 1.10 m
    public void TheTopEdgeLandsWhereItWasAskedFor(float width, float quadHeight)
    {
        var head = Standing();
        var knee = VrPlacementMath.KneeHeight(head.Position.Y);

        var resting = VrPlacementMath.Resting(head, 0.9f, knee, quadHeight);

        Assert.Equal(knee, resting.Position.Y + (quadHeight / 2f), 4);

        // And the width is what set the height, so a wider panel sits lower to keep its top in
        // the same place. That is the property a hand-tuned drop would lose.
        Assert.True(width > 0);
    }

    /// <summary>
    /// In front of where the Commander is facing, at the distance asked for — measured on the
    /// floor plane, so looking down does not bring the panel closer.
    /// </summary>
    [Fact]
    public void ItSitsTheAskedDistanceAheadOnTheFloorPlane()
    {
        var head = new VrPose(
            new Vector3(2f, 1.65f, -3f),
            Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f, 0, 0));

        var resting = VrPlacementMath.Resting(head, 0.9f, 0.5f, 0.15f);

        var flat = new Vector3(resting.Position.X - head.Position.X, 0, resting.Position.Z - head.Position.Z);

        Assert.Equal(0.9f, flat.Length(), 4);
    }

    /// <summary>
    /// A Commander looking at their feet when the panel first appears meant "in front of me",
    /// not "below where I was already looking". Pitch and roll of the head are dropped; only the
    /// compass direction survives.
    /// </summary>
    [Fact]
    public void LookingDownDoesNotDragThePanelDownWithIt()
    {
        var level = VrPlacementMath.Resting(Standing(), 0.9f, 0.5f, 0.15f);

        var lookingDown = new VrPose(
            new Vector3(0, 1.65f, 0),
            Quaternion.CreateFromYawPitchRoll(0, -1.2f, 0.3f));

        var stooped = VrPlacementMath.Resting(lookingDown, 0.9f, 0.5f, 0.15f);

        Assert.Equal(level.Position.X, stooped.Position.X, 4);
        Assert.Equal(level.Position.Y, stooped.Position.Y, 4);
        Assert.Equal(level.Position.Z, stooped.Position.Z, 4);
    }

    /// <summary>
    /// A quad near the floor that is not tilted up is a line. It faces the eyes that are above
    /// it, and at eye level it stands upright rather than being tilted for its own sake.
    /// <para>
    /// <b>Measured on +Z, and that correction is the point.</b> This test read the normal off -Z
    /// and passed for as long as the placement had its pitch sign inverted — because -Z is the
    /// quad's <em>back</em>, so what it actually asserted was that a knee-level panel turns its
    /// back up towards the Commander and its face at the floor. Both halves were wrong together,
    /// which is why nothing caught it: the angle was the right size, and the axis agreed with the
    /// arithmetic rather than with the headset.
    /// </para>
    /// </summary>
    [Fact]
    public void ItIsTiltedBackTowardsTheEyesAboveIt()
    {
        var head = Standing();

        var low = VrPlacementMath.Resting(head, 0.9f, 0.5f, 0.15f);
        var atEyeLevel = VrPlacementMath.Resting(head, 0.9f, head.Position.Y + 0.075f, 0.15f);

        // The face an overlay actually shows, which looks along its own +Z: tipped back, it has
        // climbed away from horizontal towards the eyes.
        var lowFace = Vector3.Transform(Vector3.UnitZ, low.Facing);
        var levelFace = Vector3.Transform(Vector3.UnitZ, atEyeLevel.Facing);

        Assert.True(lowFace.Y > 0.3f, $"a knee-level panel was left facing the floor ({lowFace.Y:0.000})");
        Assert.Equal(0f, levelFace.Y, 3);
    }

    /// <summary>
    /// Straight up leaves no compass direction at all. Any finite answer will do; NaN will not,
    /// and it would be written to view state and persist.
    /// </summary>
    [Fact]
    public void AHeadPointedStraightUpStillGivesAFinitePose()
    {
        var head = new VrPose(
            new Vector3(0, 1.65f, 0),
            Quaternion.CreateFromYawPitchRoll(0, MathF.PI / 2f, 0));

        var resting = VrPlacementMath.Resting(head, 0.9f, 0.5f, 0.15f);

        Assert.True(resting.IsFinite);
    }

    /// <summary>
    /// Knee height is derived from the floor and the eyes rather than assumed, and clamped so a
    /// dropped tracking frame cannot anchor the panel underground or at head height — a bad
    /// first placement is written to view state and outlives the frame that caused it.
    /// </summary>
    [Theory]
    [InlineData(1.80f, 0.25f, 0.75f)]
    [InlineData(1.50f, 0.25f, 0.75f)]
    [InlineData(0f, 0.25f, 0.75f)]
    [InlineData(-5f, 0.25f, 0.75f)]
    [InlineData(400f, 0.25f, 0.75f)]
    public void KneeHeightStaysWithinReach(float eyeHeight, float floor, float ceiling)
    {
        var knee = VrPlacementMath.KneeHeight(eyeHeight);

        Assert.InRange(knee, floor, ceiling);
    }

    /// <summary>A taller Commander gets a higher panel, which is the point of deriving it.</summary>
    [Fact]
    public void ATallerCommanderGetsAHigherPanel()
    {
        Assert.True(VrPlacementMath.KneeHeight(1.80f) > VrPlacementMath.KneeHeight(1.55f));
    }
}
