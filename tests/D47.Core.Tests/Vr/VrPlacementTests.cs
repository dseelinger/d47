using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// The placement arithmetic, with no headset and no runtime. If a headset is needed to check
/// this, the maths is in the wrong place.
/// <para>
/// Every test that can be run at an awkward head pose is, because the identity pose is where
/// wrong arithmetic hides: a transposed rotation, a mis-composed offset and an accumulated
/// delta all look perfect for as long as nothing is turned.
/// </para>
/// </summary>
public class VrPlacementTests
{
    /// <summary>
    /// Rotated on all three axes and standing somewhere that is not the origin. Nothing here
    /// agrees by symmetry.
    /// </summary>
    private static VrPose Awkward => new(
        new Vector3(1.5f, 1.7f, -3.0f),
        Quaternion.CreateFromYawPitchRoll(0.7f, -0.2f, 0.1f));

    [Fact]
    public void AHeadLockedSurfaceSitsTheConfiguredDistanceAway()
    {
        foreach (var head in new[] { VrPose.Origin, Awkward })
        {
            var placed = VrPlacementMath.HeadLocked(head, distanceMetres: 1.1f, dropMetres: 0f, pitchRadians: 0f);

            Assert.Equal(1.1f, Vector3.Distance(head.Position, placed.Position), 4);
        }
    }

    [Fact]
    public void AHeadLockedSurfaceIsInFrontOfTheHeadRatherThanInFrontOfTheWorld()
    {
        var placed = VrPlacementMath.HeadLocked(Awkward, 1.1f, 0f, 0f);

        // Forward is the head's own -Z, which is OpenVR's convention and not ours. Composed
        // the other way round the surface still moves when the head moves, so every "it
        // followed" assertion passes and the distance is what comes out wrong.
        var forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, Awkward.Facing));
        var towards = Vector3.Normalize(placed.Position - Awkward.Position);

        Assert.Equal(1f, Vector3.Dot(forward, towards), 3);
    }

    [Fact]
    public void TheDropIsMeasuredInTheHeadsOwnFrame()
    {
        // A Commander lying on their side wants the panel below their eyes, not below the
        // world. Applied in world space this would hang off to one side.
        var onTheirSide = new VrPose(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2));
        var placed = VrPlacementMath.HeadLocked(onTheirSide, 1.0f, dropMetres: -0.25f, pitchRadians: 0f);

        var down = Vector3.Transform(-Vector3.UnitY, onTheirSide.Facing);
        var offset = placed.Position - Vector3.Transform(new Vector3(0, 0, -1), onTheirSide.Facing);

        Assert.Equal(0.25f, Vector3.Dot(offset, down), 3);
    }

    /// <summary>
    /// A grab is rigid. The panel keeps its offset through a wrist <em>rotation</em>, which is
    /// what an implementation tracking only position gets wrong — and gets wrong invisibly,
    /// because translating the hand alone works perfectly either way.
    /// </summary>
    [Fact]
    public void AGrabbedSurfaceKeepsItsOffsetThroughAWristRotation()
    {
        var hand = new VrPose(new Vector3(0.2f, 1.2f, -0.4f), Quaternion.Identity);
        var surface = new VrPose(new Vector3(0.0f, 1.4f, -1.1f), Quaternion.Identity);

        var offset = VrPlacementMath.Grab(hand, surface);

        var turned = new VrPose(
            new Vector3(0.35f, 1.05f, -0.55f),
            Quaternion.CreateFromYawPitchRoll(0.6f, 0.25f, -0.4f));

        var carried = VrPlacementMath.Carried(offset, turned);

        // The panel moved. Without this the assertion below passes for an implementation that
        // simply never moves anything.
        Assert.True(Vector3.Distance(carried.Position, surface.Position) > 0.1f);

        // And it is still the same distance from the hand, in the hand's own frame.
        Assert.Equal(
            Vector3.Distance(hand.Position, surface.Position),
            Vector3.Distance(turned.Position, carried.Position),
            4);
    }

    [Fact]
    public void AGrabThatHasNotMovedLeavesTheSurfaceExactlyWhereItWas()
    {
        var hand = Awkward;
        var surface = new VrPose(new Vector3(0.4f, 1.3f, -0.9f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f));

        var carried = VrPlacementMath.Carried(VrPlacementMath.Grab(hand, surface), hand);

        Assert.Equal(surface.Position.X, carried.Position.X, 4);
        Assert.Equal(surface.Position.Y, carried.Position.Y, 4);
        Assert.Equal(surface.Position.Z, carried.Position.Z, 4);
        Assert.True(Math.Abs(Quaternion.Dot(surface.Facing, carried.Facing)) > 0.9999f);
    }

    /// <summary>
    /// The whole reason re-anchor takes a group rather than a pose: Elite's recenter turns the
    /// cockpit, and putting every panel back means turning them all by the same amount, not
    /// stacking them all in front of the Commander.
    /// </summary>
    [Fact]
    public void ReanchoringMovesEverySurfaceTogetherAndPreservesTheirLayout()
    {
        var anchoredAt = new VrPose(new Vector3(0, 1.6f, 0), Quaternion.Identity);

        var left = new VrPose(new Vector3(-0.8f, 1.5f, -1.2f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f));
        var right = new VrPose(new Vector3(0.8f, 1.5f, -1.2f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.4f));

        // The Commander has turned ninety degrees and stepped sideways.
        var headNow = new VrPose(
            new Vector3(0.5f, 1.6f, 0.3f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        var movedLeft = VrPlacementMath.Reanchored(left, anchoredAt, headNow);
        var movedRight = VrPlacementMath.Reanchored(right, anchoredAt, headNow);

        // Same distance apart as before. Stacking them is precisely what a naive "put it back
        // where it started" does.
        Assert.Equal(
            Vector3.Distance(left.Position, right.Position),
            Vector3.Distance(movedLeft.Position, movedRight.Position),
            4);

        // And both the same distance from the head as they were.
        Assert.Equal(
            Vector3.Distance(anchoredAt.Position, left.Position),
            Vector3.Distance(headNow.Position, movedLeft.Position),
            4);

        // Both now in front of the Commander rather than behind them.
        var forward = Vector3.Transform(-Vector3.UnitZ, headNow.Facing);
        Assert.True(Vector3.Dot(movedLeft.Position - headNow.Position, forward) > 0);
        Assert.True(Vector3.Dot(movedRight.Position - headNow.Position, forward) > 0);
    }

    [Fact]
    public void ReanchoringAgainstTheSameHeadPoseChangesNothing()
    {
        var head = Awkward;
        var surface = new VrPose(new Vector3(0.2f, 1.4f, -1.0f), Quaternion.Identity);

        var again = VrPlacementMath.Reanchored(surface, head, head);

        Assert.Equal(surface.Position.X, again.Position.X, 4);
        Assert.Equal(surface.Position.Y, again.Position.Y, 4);
        Assert.Equal(surface.Position.Z, again.Position.Z, 4);
    }

    /// <summary>
    /// Yaw only. A Commander who happened to be looking at their feet when they re-anchored
    /// did not mean to tip every panel forward and hang them over their knees.
    /// </summary>
    [Fact]
    public void ReanchoringDoesNotInheritTheCommandersPitchOrRoll()
    {
        var anchoredAt = new VrPose(new Vector3(0, 1.6f, 0), Quaternion.Identity);
        var surface = new VrPose(new Vector3(0, 1.5f, -1.2f), Quaternion.Identity);

        var lookingDown = new VrPose(
            new Vector3(0, 1.6f, 0),
            Quaternion.CreateFromYawPitchRoll(0f, -0.9f, 0.3f));

        var moved = VrPlacementMath.Reanchored(surface, anchoredAt, lookingDown);

        Assert.Equal(surface.Position.Y, moved.Position.Y, 3);
    }

    /// <summary>
    /// A quaternion out of a tracking runtime drifts off unit length over a session, and one
    /// that is one per cent long scales everything it touches — so the panel creeps away from
    /// the Commander the longer they play.
    /// </summary>
    [Fact]
    public void ADriftedOrientationIsNormalisedRatherThanScalingEverythingItTouches()
    {
        var drifted = new VrPose(Vector3.Zero, new Quaternion(0, 0.1f, 0, 1.01f));

        Assert.Equal(1f, drifted.Facing.Length(), 5);
        Assert.Equal(1.1f, Vector3.Distance(
            Vector3.Zero,
            VrPlacementMath.HeadLocked(drifted, 1.1f, 0, 0).Position), 4);
    }

    [Fact]
    public void AZeroOrientationIsTreatedAsNoRotationRatherThanAsNaN()
    {
        var zeroed = new VrPose(Vector3.Zero, new Quaternion(0, 0, 0, 0));

        Assert.Equal(Quaternion.Identity, zeroed.Facing);
    }

    [Fact]
    public void ADroppedTrackingFrameIsRecognisableRatherThanCarriedThrough()
    {
        // Every arithmetic path here will happily carry a NaN through to a transform, where it
        // becomes an overlay that is nowhere.
        Assert.False(new VrPose(new Vector3(float.NaN, 0, 0), Quaternion.Identity).IsFinite);
        Assert.False(new VrPose(Vector3.Zero, new Quaternion(0, float.PositiveInfinity, 0, 1)).IsFinite);
        Assert.True(VrPose.Origin.IsFinite);
    }

    [Fact]
    public void APlacementFromAHandEditedFileIsClampedRatherThanObeyed()
    {
        var absurd = new SurfacePlacement
        {
            DistanceMetres = 400f,
            WidthMetres = 0f,
            Curvature = 9f,
            Opacity = -3f,
            ZoomPercent = 137,
        }.Sane();

        Assert.Equal(5f, absurd.DistanceMetres);
        Assert.True(absurd.WidthMetres > 0, "a width of zero is an overlay that is there and invisible");
        Assert.Equal(1f, absurd.Curvature);
        Assert.True(absurd.Opacity is > 0 and <= 1);
        Assert.Equal(125, absurd.ZoomPercent);
    }

    [Fact]
    public void AWorldLockedSurfaceThatHasNeverBeenPutDownFallsBackToWhereTheHeadIs()
    {
        var never = new SurfacePlacement { Lock = SurfaceLock.WorldLocked };

        // Otherwise the first thing the Commander sees is a panel at the tracking origin,
        // which is usually behind them.
        var placed = never.Where(Awkward);

        // The distance is along the head's forward; the surface also sits below eye level, so
        // the straight-line distance is longer than the setting and should be.
        var forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, Awkward.Facing));
        Assert.Equal(
            never.DistanceMetres,
            Vector3.Dot(placed.Position - Awkward.Position, forward),
            3);
    }
}
