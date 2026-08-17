using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// Where a controller ray meets the panel, and where the beam and cursor go as a result.
/// <para>
/// All of this replaced questions the runtime used to be asked, so all of it is now d47's to get
/// wrong — and every one of these is the kind of wrong that looks perfect until something is
/// turned. The panel poses below are deliberately awkward for that reason.
/// </para>
/// </summary>
public class VrRayTests
{
    /// <summary>A panel a metre and a bit ahead, dropped and tilted the way a real one is.</summary>
    private static VrPose Panel => VrPlacementMath.HeadLocked(VrPose.Origin, 1.1f, -0.25f, 0f);

    private static VrExtent Extent => new(1.1f, 1024f / 640f);

    /// <summary>A hand at the eye, aiming at a point on the panel.</summary>
    private static VrHand AimingAt(Vector3 target, Vector3? from = null)
    {
        var origin = from ?? new Vector3(0.15f, -0.05f, 0f);
        var direction = Vector3.Normalize(target - origin);

        // A pose whose -Z is the direction, which is what the ray is cast along.
        var facing = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateWorld(origin, direction, Vector3.UnitY));

        return new VrHand(1, new VrPose(origin, facing), new VrPose(origin, facing));
    }

    [Fact]
    public void ARayAtTheCentreLandsInTheMiddle()
    {
        var hit = VrRay.Hit(Panel, Extent, 0f, AimingAt(Panel.Position).Aim);

        Assert.NotNull(hit);
        Assert.Equal(0.5f, hit!.Value.U, 3);
        Assert.Equal(0.5f, hit.Value.V, 3);
    }

    /// <summary>
    /// V reads from the <em>top</em>, matching raster order rather than OpenVR's Y-up. Left
    /// unflipped the panel works perfectly upside down, which is the sort of thing that survives
    /// every test that only checks whether a ray hit at all.
    /// </summary>
    [Fact]
    public void TheVerticalAxisReadsFromTheTop()
    {
        var up = Vector3.Transform(Vector3.UnitY, Panel.Facing);

        var high = VrRay.Hit(Panel, Extent, 0f, AimingAt(Panel.Position + (up * 0.15f)).Aim);
        var low = VrRay.Hit(Panel, Extent, 0f, AimingAt(Panel.Position - (up * 0.15f)).Aim);

        Assert.NotNull(high);
        Assert.NotNull(low);
        Assert.True(high!.Value.V < 0.5f, $"a point above the centre read as v={high.Value.V:0.000}");
        Assert.True(low!.Value.V > 0.5f, $"a point below the centre read as v={low.Value.V:0.000}");
    }

    /// <summary>
    /// U runs left to right across the face as the Commander sees it, which is the other half of
    /// the same mirror.
    /// </summary>
    [Fact]
    public void TheHorizontalAxisReadsFromTheLeft()
    {
        // The panel's own +X is the viewer's left, because the face they see looks back at them.
        var right = Vector3.Transform(Vector3.UnitX, Panel.Facing);

        var hit = VrRay.Hit(Panel, Extent, 0f, AimingAt(Panel.Position + (right * 0.3f)).Aim);

        Assert.NotNull(hit);
        Assert.True(hit!.Value.U > 0.5f);
    }

    /// <summary>
    /// A ray arriving at the back is not a hit. Returning one would let a Commander with a hand
    /// behind the panel grab it through its own back.
    /// </summary>
    [Fact]
    public void ARayArrivingAtTheBackMisses()
    {
        var behind = Panel.Position + Vector3.Transform(-Vector3.UnitZ, Panel.Facing);

        Assert.Null(VrRay.Hit(Panel, Extent, 0f, AimingAt(Panel.Position, behind).Aim));
    }

    /// <summary>And a ray pointing away from the panel entirely is not a hit either.</summary>
    [Fact]
    public void ARayPointingAwayMisses()
    {
        var away = AimingAt(new Vector3(0, 0, 5f));

        Assert.Null(VrRay.Hit(Panel, Extent, 0f, away.Aim));
    }

    [Fact]
    public void ARayPastTheEdgeMisses()
    {
        var right = Vector3.Transform(Vector3.UnitX, Panel.Facing);

        Assert.Null(VrRay.Hit(Panel, Extent, 0f, AimingAt(Panel.Position + (right * 2f)).Aim));
    }

    /// <summary>
    /// The curved model is the flat one's limit rather than a separate case, so the two have to
    /// agree as curvature goes to zero. If they do not, a Commander who flattens the panel sees
    /// the cursor jump.
    /// </summary>
    [Fact]
    public void TheCurvedAndFlatModelsAgreeAsCurvatureVanishes()
    {
        var right = Vector3.Transform(Vector3.UnitX, Panel.Facing);
        var ray = AimingAt(Panel.Position + (right * 0.2f)).Aim;

        var flat = VrRay.Hit(Panel, Extent, 0f, ray);
        var barely = VrRay.Hit(Panel, Extent, 0.0011f, ray);

        Assert.NotNull(flat);
        Assert.NotNull(barely);
        Assert.Equal(flat!.Value.U, barely!.Value.U, 2);
        Assert.Equal(flat.Value.V, barely.Value.V, 2);
    }

    /// <summary>A real curvature still finds the middle, which is on the cylinder either way.</summary>
    [Fact]
    public void ACurvedPanelIsStillHitAtItsCentre()
    {
        var hit = VrRay.Hit(Panel, Extent, 0.25f, AimingAt(Panel.Position).Aim);

        Assert.NotNull(hit);
        Assert.Equal(0.5f, hit!.Value.U, 2);
        Assert.Equal(0.5f, hit.Value.V, 2);
    }

    /// <summary>
    /// Nearest hit wins, and the hand that wins is the one that gets to carry the panel. Both
    /// hands are aimed at the panel here, so device order cannot be what decides it.
    /// </summary>
    [Fact]
    public void TheNearerHandIsTheOnePointing()
    {
        var far = AimingAt(Panel.Position, new Vector3(-0.3f, 0f, 0.4f)) with { Device = 7 };
        var near = AimingAt(Panel.Position, new Vector3(0.3f, 0f, -0.4f)) with { Device = 9 };

        var found = VrRay.PointingAt([far, near], Panel, Extent, 0f);

        Assert.NotNull(found);
        Assert.Equal(9u, found!.Value.Hand.Device);
    }

    /// <summary>Nothing aimed at it is nobody pointing, rather than an arbitrary hand.</summary>
    [Fact]
    public void NoHandAimedAtThePanelIsNobodyPointing()
    {
        var away = AimingAt(new Vector3(0, 0, 5f));

        Assert.Null(VrRay.PointingAt([away], Panel, Extent, 0f));
    }

    /// <summary>
    /// The beam lies along the aim and turns its face to the viewer. Edge-on it would be
    /// invisible, which is exactly the angle a beam pointing away tends to be seen at.
    /// </summary>
    [Fact]
    public void TheBeamLiesAlongTheAimAndFacesTheHead()
    {
        var hand = AimingAt(Panel.Position);

        // Well off the beam's own axis, or "turned towards the head" is a rotation about nothing.
        var head = new Vector3(-0.6f, 0.5f, 0.3f);

        var beam = VrAim.BeamAlong(hand.Aim, head, 1.2f);

        var along = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, hand.Aim.Facing));

        // The texture is tall rather than wide, so the quad's long axis is its own +Y.
        var length = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, beam.Facing));
        var face = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, beam.Facing));

        Assert.Equal(1f, Vector3.Dot(length, along), 3);

        // The face can only be the part of "towards the head" that is square to the beam — the
        // long axis is already spoken for — so the property is that it is perpendicular to the
        // beam and leans the head's way, not that it points straight at them.
        Assert.Equal(0f, Vector3.Dot(face, length), 3);
        Assert.True(
            Vector3.Dot(face, Vector3.Normalize(head - beam.Position)) > 0f,
            "the beam rolled its face away from the head, which is the angle it is invisible at");

        // And it is centred half its length along, so it starts at the hand and ends at the point.
        Assert.Equal(0.6f, Vector3.Distance(beam.Position, hand.Aim.Position), 3);
    }

    /// <summary>
    /// The beam stops exactly where the cursor sits, because one distance feeds both. If they
    /// disagree the beam visibly ends short of, or past, the dot it is supposed to be touching.
    /// </summary>
    [Fact]
    public void TheBeamEndsWhereTheCursorSits()
    {
        var hand = AimingAt(Panel.Position);
        var hit = VrRay.Hit(Panel, Extent, 0f, hand.Aim);

        Assert.NotNull(hit);

        var point = VrAim.PointAlong(hand.Aim, hit!.Value.DistanceMetres);
        var beam = VrAim.BeamAlong(hand.Aim, Vector3.Zero, hit.Value.DistanceMetres);

        var along = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, beam.Facing));
        var end = beam.Position + (along * (hit.Value.DistanceMetres / 2f));

        Assert.Equal(0f, Vector3.Distance(end, point), 3);
    }

    /// <summary>The cursor turns its face at the eye and is lifted towards it, never into the glass.</summary>
    [Fact]
    public void TheCursorFacesTheHeadAndSitsInFrontOfTheSurface()
    {
        var head = new Vector3(0.1f, 0.05f, 0.2f);
        var point = Panel.Position;

        var cursor = VrAim.CursorAt(point, head);

        var face = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, cursor.Facing));

        Assert.Equal(1f, Vector3.Dot(face, Vector3.Normalize(head - point)), 3);
        Assert.Equal(VrAim.CursorLiftMetres, Vector3.Distance(cursor.Position, point), 4);
        Assert.True(Vector3.Distance(cursor.Position, head) < Vector3.Distance(point, head));
    }

    /// <summary>
    /// A head directly along the beam leaves no perpendicular to roll towards. It has to degrade
    /// to something finite rather than a zero vector, which would make the quad vanish or the
    /// quaternion turn into NaN and persist.
    /// </summary>
    [Fact]
    public void AHeadStraightDownTheBeamStillGivesAFinitePose()
    {
        var hand = AimingAt(new Vector3(0, 0, -2f), new Vector3(0, 0, 0));
        var beam = VrAim.BeamAlong(hand.Aim, hand.Aim.Position, 1f);

        Assert.True(beam.IsFinite);
    }

    /// <summary>And a cursor exactly under the eye has no "up" to cross against.</summary>
    [Fact]
    public void ACursorDirectlyBelowTheEyeStillGivesAFinitePose()
    {
        var cursor = VrAim.CursorAt(new Vector3(0, 0, 0), new Vector3(0, 1.7f, 0));

        Assert.True(cursor.IsFinite);
    }
}
