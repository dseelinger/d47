using System.Numerics;
using D47.Core.Hulls;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The hull camera reaches every orientation, frames the hull at rest and keeps its zoom, light and pan within bounds.</summary>
public class AHullCameraOrbitsTheHullTests
{
    private const float Pixels90 = 90f / HullCamera.DegreesPerPixel;

    private static HullMesh Box()
    {
        Vector3[] corners = [.. Enumerable.Range(0, 8).Select(i => new Vector3(
            (i & 1) == 0 ? -3 : 3, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -2 : 2))];
        var radius = corners.Max(c => c.Length());
        return new HullMesh(corners, corners, [0, 1, 2], [new HullPart("hull", 0, 1, false)], radius, Vector3.Normalize(new Vector3(0.3f, 0.5f, 0.8f)));
    }

    private static Vector3 Facing(HullCamera c) => Vector3.Transform(Vector3.UnitZ, c.Orbit);

    private static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.True((expected - actual).Length() < 1e-4f, $"expected {expected}, got {actual}");
    }

    [Fact]
    public void FourQuarterTurnsAboutEachAxisReturnToTheStart()
    {
        var start = HullCamera.Rest(Box());
        var yaw = start;
        var pitch = start;
        var roll = start;
        for (var i = 0; i < 4; i++)
        {
            yaw = yaw.Turn(Pixels90, 0);
            pitch = pitch.Turn(0, Pixels90);
            roll = roll.Roll(Pixels90);
        }

        foreach (var c in new[] { yaw, pitch, roll })
        {
            Assert.True(MathF.Abs(Quaternion.Dot(start.Orbit, c.Orbit)) > 0.9999f);
        }
    }

    [Fact]
    public void ATurnAboutXThenYThenARollFacesTheExpectedWay()
    {
        var c = new HullCamera(Quaternion.Identity, 10, Vector3.Zero, 1, 1);

        c = c.Turn(0, Pixels90);
        Near(new Vector3(0, 1, 0), Facing(c));
        c = c.Turn(Pixels90, 0);
        Near(new Vector3(-1, 0, 0), Facing(c));
        c = c.Roll(Pixels90);
        Near(new Vector3(-1, 0, 0), Facing(c));
        Near(new Vector3(0, -1, 0), Vector3.Transform(Vector3.UnitY, c.Orbit));
    }

    [Fact]
    public void TheRestViewFitsEveryCornerOfTheBoundsInClipSpace()
    {
        var mesh = Box();
        var c = HullCamera.Rest(mesh);
        var viewProjection = c.View() * c.Projection(1f);

        foreach (var p in mesh.Positions)
        {
            var clip = Vector4.Transform(new Vector4(p, 1), viewProjection);
            Assert.InRange(clip.X / clip.W, -1f, 1f);
            Assert.InRange(clip.Y / clip.W, -1f, 1f);
            Assert.InRange(clip.Z / clip.W, 0f, 1f);
        }
    }

    [Fact]
    public void TheRestViewLooksFromTheMeshsRestDirection()
    {
        var mesh = Box();

        Near(mesh.RestView, Facing(HullCamera.Rest(mesh)));
    }

    [Fact]
    public void ZoomAndRelightStopAtTheirClamps()
    {
        var c = HullCamera.Rest(Box());

        Assert.Equal(c.Radius * 0.2f, c.Zoom(1000).Distance, 3);
        Assert.Equal(c.Radius * 10f, c.Zoom(-1000).Distance, 3);
        Assert.Equal(c.Distance / 1.15f, c.Zoom(1).Distance, 3);
        Assert.Equal(2f, c.Relight(5).Light);
        Assert.Equal(0f, c.Relight(-5).Light);
    }

    [Fact]
    public void APanKeepsThePointUnderTheCursorUnderIt()
    {
        const int height = 800;
        var c = HullCamera.Rest(Box()).Turn(40, 25);
        var point = c.Target;
        var before = Screen(c, point, height);

        var panned = c.Pan(50, -30, height);
        var after = Screen(panned, point, height);

        Assert.Equal(before.X + 50, after.X, 1);
        Assert.Equal(before.Y - 30, after.Y, 1);
    }

    private static Vector2 Screen(HullCamera c, Vector3 world, int height)
    {
        var clip = Vector4.Transform(new Vector4(world, 1), c.View() * c.Projection(1f));
        return new Vector2((clip.X / clip.W + 1) / 2 * height, (1 - clip.Y / clip.W) / 2 * height);
    }
}
