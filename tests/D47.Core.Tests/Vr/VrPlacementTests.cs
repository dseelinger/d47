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
            var placed = VrPlacementMath.HeadLocked(head, distanceMetres: 1.1f, dropMetres: 0f, pitchTrimRadians: 0f);

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
        var placed = VrPlacementMath.HeadLocked(onTheirSide, 1.0f, dropMetres: -0.25f, pitchTrimRadians: 0f);

        var down = Vector3.Transform(-Vector3.UnitY, onTheirSide.Facing);
        var offset = placed.Position - Vector3.Transform(new Vector3(0, 0, -1), onTheirSide.Facing);

        Assert.Equal(0.25f, Vector3.Dot(offset, down), 3);
    }

    /// <summary>
    /// The tilt is worked out from the geometry, not taken from a setting. A fixed angle can
    /// only be right for one distance and drop, and the two panels ship with two of each.
    /// </summary>
    [Theory]
    [InlineData(1.1f, -0.25f, -12.8f)]  // the full panel: the magnitude the old fixed 12° was tuned for
    [InlineData(0.9f, -0.30f, -18.4f)]  // mini: six degrees short under the old constant
    [InlineData(1.6f, 0f, 0f)]          // at eye level there is nothing to tilt towards
    public void TheHeadLockedTiltFacesTheEyesFromTheDistanceAndDrop(
        float distance,
        float drop,
        float expectedDegrees)
    {
        var degrees = VrPlacementMath.EyeFacingPitch(distance, drop) * 180f / MathF.PI;

        Assert.Equal(expectedDegrees, degrees, 1);
    }

    /// <summary>
    /// The derived tilt actually points the quad's face at the eye, rather than merely being a
    /// number of the right size. Checked as the angle between the surface's own forward and the
    /// line from the surface to the head, which is zero when it is aimed exactly at them.
    /// </summary>
    [Theory]
    [InlineData(1.1f, -0.25f)]
    [InlineData(0.9f, -0.30f)]
    [InlineData(1.4f, -0.60f)]
    public void AHeadLockedSurfaceAimsItsFaceAtTheEye(float distance, float drop)
    {
        foreach (var head in new[] { VrPose.Origin, Awkward })
        {
            var placed = VrPlacementMath.HeadLocked(head, distance, drop, pitchTrimRadians: 0f);

            // The visible face of an overlay quad looks along its own +Z, which is the opposite
            // of the -Z every tracked device points along.
            var face = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, placed.Facing));
            var toTheEye = Vector3.Normalize(head.Position - placed.Position);

            Assert.Equal(1f, Vector3.Dot(face, toTheEye), 3);
        }
    }

    /// <summary>
    /// The same question asked of the world-locked resting placement, which shipped with this sign
    /// inverted: a panel dropped to knee height tilted its face at the floor, through twice the
    /// angle it should have gone the other way. An assertion on the angle cannot see that — it is
    /// the right size either way — so this one is on the direction the face ends up pointing.
    /// </summary>
    [Theory]
    [InlineData(1.7f, 0.5f)]    // standing: the panel rests near the knee, well below the eye
    [InlineData(1.2f, 0.4f)]    // seated: less far below, and still below
    public void ARestingSurfaceAimsItsFaceAtTheEye(float eyeHeight, float topEdge)
    {
        var head = new VrPose(new Vector3(0, eyeHeight, 0), Quaternion.Identity);
        var placed = VrPlacementMath.Resting(head, distanceMetres: 1.1f, topEdge, quadHeightMetres: 0.6f);

        var face = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, placed.Facing));
        var toTheEye = Vector3.Normalize(head.Position - placed.Position);

        Assert.Equal(1f, Vector3.Dot(face, toTheEye), 3);
    }

    /// <summary>
    /// The trim is added to the derived angle rather than replacing it, so a Commander who nudges
    /// it moves the panel by that much from facing them — not to that much from square.
    /// </summary>
    [Fact]
    public void TheConfiguredPitchTrimsTheDerivedTiltRatherThanReplacingIt()
    {
        var trim = 5f * MathF.PI / 180f;

        var facing = VrPlacementMath.HeadLocked(VrPose.Origin, 1.1f, -0.25f, 0f);
        var trimmed = VrPlacementMath.HeadLocked(VrPose.Origin, 1.1f, -0.25f, trim);

        var between = Quaternion.Concatenate(Quaternion.Inverse(facing.Facing), trimmed.Facing);

        Assert.Equal(5f, 2f * MathF.Acos(MathF.Abs(between.W)) * 180f / MathF.PI, 2);
    }

    /// <summary>
    /// Captions opt out, and are the only thing that does: they sit 0.45 m below the eye at
    /// 1.6 m, so deriving would tilt them 15.7° when they are meant to be square to the view.
    /// </summary>
    [Fact]
    public void ASurfaceThatDoesNotFaceTheEyesKeepsItsAngleOutright()
    {
        var placed = VrPlacementMath.HeadLocked(
            VrPose.Origin, 1.6f, -0.45f, pitchTrimRadians: 0f, facesTheEyes: false);

        Assert.Equal(Quaternion.Identity, placed.Facing, new QuaternionComparer());
    }

    private sealed class QuaternionComparer : IEqualityComparer<Quaternion>
    {
        public bool Equals(Quaternion a, Quaternion b) =>
            MathF.Abs(MathF.Abs(Quaternion.Dot(a, b)) - 1f) < 1e-4f;

        public int GetHashCode(Quaternion value) => 0;
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
