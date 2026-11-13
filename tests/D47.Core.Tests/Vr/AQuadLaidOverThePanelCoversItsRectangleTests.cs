using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>A quad laid over part of the panel sits on that part, in front of it, wherever the panel is.</summary>
public class AQuadLaidOverThePanelCoversItsRectangleTests
{
    private static readonly (int Width, int Height) Pixels = (1250, 690);

    /// <summary>Where the big panel draws the avatar.</summary>
    private static readonly VrPixelRect Avatar = new(1118, 36, 104, 104);

    private static readonly VrPose Head = new(new Vector3(0.1f, 1.2f, 0.3f), Quaternion.CreateFromYawPitchRoll(0.4f, -0.1f, 0f));

    public static TheoryData<string> Panels => ["flat", "curved", "world-locked", "world-locked curved"];

    private static SurfacePlacement PanelFor(string kind)
    {
        var panel = new SurfacePlacement { WidthMetres = 1.0f, Opacity = 0.8f };

        if (kind.Contains("curved"))
        {
            panel = panel with { Curvature = 0.2f };
        }

        if (kind.StartsWith("world-locked"))
        {
            panel = panel with
            {
                Lock = SurfaceLock.WorldLocked,
                Placed = VrPlacementMath.Resting(Head, 0.9f, 1.3f, 0.55f),
            };
        }

        return panel.Sane();
    }

    /// <summary>
    /// Straight back through the quad's centre along its normal, from half a metre in front of it. A ray from the
    /// head would land a few pixels off by parallax with the quad's lift.
    /// </summary>
    private static (VrHit Hit, float QuadDistance) LookThroughTheQuad(SurfacePlacement panel, VrPixelRect rect)
    {
        const float Back = 0.5f;

        var quad = VrPlacementMath.Over(panel, Pixels, rect).Sane().Where(Head);
        var forward = Vector3.Transform(Vector3.UnitZ, quad.Facing);

        var hit = VrRay.Hit(
            panel.Where(Head),
            new VrExtent(panel.WidthMetres, Pixels.Width / (float)Pixels.Height),
            panel.Curvature,
            quad.Position + (forward * Back),
            -forward);

        Assert.NotNull(hit);
        return (hit!.Value, Back);
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public void ALookThroughTheQuadsCentreLandsOnTheRectanglesCentre(string kind)
    {
        var (hit, _) = LookThroughTheQuad(PanelFor(kind), Avatar);

        Assert.InRange(hit.U * Pixels.Width, Avatar.X + (Avatar.Width / 2f) - 1f, Avatar.X + (Avatar.Width / 2f) + 1f);
        Assert.InRange(hit.V * Pixels.Height, Avatar.Y + (Avatar.Height / 2f) - 1f, Avatar.Y + (Avatar.Height / 2f) + 1f);
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public void TheQuadIsInFrontOfThePanel(string kind)
    {
        var (hit, quadDistance) = LookThroughTheQuad(PanelFor(kind), Avatar);

        Assert.True(quadDistance < hit.DistanceMetres, $"quad at {quadDistance:0.0000} m, panel at {hit.DistanceMetres:0.0000} m");
    }

    [Theory]
    [MemberData(nameof(Panels))]
    public void TheQuadIsAsWideAsItsRectangleAndCurvedLikeThePanel(string kind)
    {
        var panel = PanelFor(kind);
        var quad = VrPlacementMath.Over(panel, Pixels, Avatar).Sane();

        Assert.Equal(panel.WidthMetres * Avatar.Width / Pixels.Width, quad.WidthMetres, 5);
        Assert.Equal(panel.Curvature * Avatar.Width / Pixels.Width, quad.Curvature, 5);

        if (panel.Curvature == 0f)
        {
            Assert.Equal(0f, quad.Curvature);
        }
        else
        {
            Assert.Equal(
                VrRay.RadiusFor(panel.WidthMetres, panel.Curvature),
                VrRay.RadiusFor(quad.WidthMetres, quad.Curvature),
                3);
        }

        Assert.Equal(panel.Opacity, quad.Opacity);
        Assert.Equal(panel.Lock, quad.Lock);
    }

    [Theory]
    [InlineData(0f, 0.5f)]
    [InlineData(0f, 0.1f)]
    [InlineData(0f, 0.9f)]
    [InlineData(-0.98f, 0.5f)]
    [InlineData(1f, 0.5f)]
    [InlineData(-0.98f, 0.1f)]
    [InlineData(1f, 0.9f)]
    public void AMiniPanelsWholeAvatarQuadIsInFrontOfTheCurve(float side, float down)
    {
        var mini = (Width: 792, Height: 280);
        var rect = new VrPixelRect(0, 0, 280, 280);
        var panel = new SurfacePlacement { WidthMetres = 0.53f, Opacity = 0.8f, Curvature = 0.1f }.Sane();
        var quad = VrPlacementMath.Over(panel, mini, rect).Sane();
        var quadPose = quad.Where(Head);

        // A point on the quad's surface: across its width and down its height, bent to its own radius.
        var radius = VrRay.RadiusFor(quad.WidthMetres, quad.Curvature);
        var angle = side * (quad.WidthMetres / 2f) / radius;
        var local = new Vector3(radius * MathF.Sin(angle), (0.5f - down) * quad.WidthMetres, radius * (1f - MathF.Cos(angle)));
        var point = Vector3.Transform(local, quadPose.ToMatrix());

        // Look straight back at the panel along its own radius through that point.
        var panelPose = panel.Where(Head);
        var panelRadius = VrRay.RadiusFor(panel.WidthMetres, panel.Curvature);
        var inLocal = Vector3.Transform(point, Matrix4x4.Invert(panelPose.ToMatrix(), out var inverse) ? inverse : throw new InvalidOperationException());
        var phi = MathF.Atan2(inLocal.X, panelRadius - inLocal.Z);
        var inward = Vector3.TransformNormal(new Vector3(-MathF.Sin(phi), 0f, MathF.Cos(phi)), panelPose.ToMatrix());

        var hit = VrRay.Hit(
            panelPose,
            new VrExtent(panel.WidthMetres, mini.Width / (float)mini.Height),
            panel.Curvature,
            point + (inward * 0.5f),
            -inward);

        Assert.NotNull(hit);
        Assert.True(0.5f < hit!.Value.DistanceMetres, $"quad at 0.5 m, panel at {hit.Value.DistanceMetres:0.0000} m");
    }

    [Fact]
    public void TheWholePanelGivesThePanelsOwnPosePushedForward()
    {
        var panel = PanelFor("flat");
        var pose = panel.Where(Head);
        var quad = VrPlacementMath.Over(panel, Pixels, new VrPixelRect(0, 0, Pixels.Width, Pixels.Height)).Sane().Where(Head);

        var forward = Vector3.Transform(Vector3.UnitZ, pose.Facing);
        var expected = pose.Position + (forward * VrPlacementMath.OverLiftMetres);

        Assert.True(Vector3.Distance(expected, quad.Position) < 1e-4f, $"off by {Vector3.Distance(expected, quad.Position):0.00000} m");
        Assert.True(MathF.Abs(Quaternion.Dot(pose.Facing, quad.Facing)) > 0.99999f);
    }

    [Fact]
    public void AQuadOverTheCurveFacesTheCurvesAxis()
    {
        var panel = PanelFor("curved");
        var pose = panel.Where(Head);
        var quad = VrPlacementMath.Over(panel, Pixels, Avatar).Sane().Where(Head);

        var radius = VrRay.RadiusFor(panel.WidthMetres, panel.Curvature);
        var axisPoint = Vector3.Transform(new Vector3(0f, 0f, radius), pose.ToMatrix());
        var toAxis = axisPoint - quad.Position;
        var up = Vector3.Transform(Vector3.UnitY, pose.Facing);
        var level = Vector3.Normalize(toAxis - (up * Vector3.Dot(toAxis, up)));

        Assert.True(Vector3.Dot(Vector3.Transform(Vector3.UnitZ, quad.Facing), level) > 0.9999f);
    }

    [Fact]
    public void AnOverlaidQuadIsNotWidenedToAPanelsNarrowest()
    {
        var quad = VrPlacementMath.Over(PanelFor("flat"), Pixels, Avatar).Sane();

        Assert.True(quad.WidthMetres < 0.15f);
        Assert.Equal(0.15f, new SurfacePlacement { WidthMetres = 0.05f }.Sane().WidthMetres);
    }
}
