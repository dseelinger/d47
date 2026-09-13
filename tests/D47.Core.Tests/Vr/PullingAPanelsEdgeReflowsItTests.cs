using System.Numerics;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>A resize drag in the headset: the held edges follow the ray and the pixels follow the size (#107).</summary>
public class PullingAPanelsEdgeReflowsItTests
{
    /// <summary>A metre across, 1000x500 pixels, at the origin facing +Z.</summary>
    private static readonly VrPanelShape Panel = new(VrPose.Origin, 1f, 1000, 500);

    /// <summary>A ray a metre in front of the panel, pointing straight at it.</summary>
    private static VrPose At(float x, float y) => new(new Vector3(x, y, 1f), Quaternion.Identity);

    private static VrPanelShape Pull(VrHandle handle, (float X, float Y) from, (float X, float Y) to)
    {
        var grab = VrResize.Grab(Panel, handle, At(from.X, from.Y));

        Assert.NotNull(grab);

        return VrResize.Drag(grab, At(to.X, to.Y));
    }

    [Fact]
    public void TheRightEdgeGrowsTheWidthAndTheLeftEdgeStaysPut()
    {
        var shape = Pull(VrHandle.Right, (0.5f, 0f), (0.7f, 0f));

        Assert.Equal(1.2f, shape.WidthMetres, 3);
        Assert.Equal(-0.5f, shape.Pose.Position.X - (shape.WidthMetres / 2f), 3);
        Assert.Equal(0.5f, shape.HeightMetres, 2);
    }

    /// <summary>More room is more rows, not bigger rows: the pixels grow with the metres.</summary>
    [Fact]
    public void ThePixelsFollowTheSizeAtTheDensityThePanelStartedWith()
    {
        var shape = Pull(VrHandle.Right, (0.5f, 0f), (0.7f, 0f));

        Assert.Equal((1200, 500), (shape.PixelsWide, shape.PixelsTall));
    }

    [Fact]
    public void ACornerMovesBothDimensionsAndLeavesTheOppositeCornerWhereItWas()
    {
        var shape = Pull(VrHandle.Top | VrHandle.Right, (0.5f, 0.25f), (0.6f, 0.35f));

        Assert.Equal(1.1f, shape.WidthMetres, 3);
        Assert.Equal(0.6f, shape.HeightMetres, 2);
        Assert.Equal(-0.5f, shape.Pose.Position.X - (shape.WidthMetres / 2f), 2);
        Assert.Equal(-0.25f, shape.Pose.Position.Y - (shape.HeightMetres / 2f), 2);
    }

    [Fact]
    public void TheBottomEdgeGrowsDownwards()
    {
        var shape = Pull(VrHandle.Bottom, (0f, -0.25f), (0f, -0.45f));

        Assert.Equal(1f, shape.WidthMetres, 3);
        Assert.Equal(0.7f, shape.HeightMetres, 2);
        Assert.Equal(0.25f, shape.Pose.Position.Y + (shape.HeightMetres / 2f), 2);
    }

    /// <summary>A panel resized to nothing could not be grabbed again to undo it.</summary>
    [Fact]
    public void ADragPastTheFloorStopsAtIt()
    {
        var shape = Pull(VrHandle.Right, (0.5f, 0f), (-10f, 0f));

        Assert.Equal(VrResize.NarrowestMetres, shape.WidthMetres, 3);
        Assert.True(shape.HeightMetres >= VrResize.ShortestMetres);
        Assert.True(shape.PixelsWide >= VrResize.FewestPixels.Width);
        Assert.True(shape.PixelsTall >= VrResize.FewestPixels.Height);
    }

    [Fact]
    public void ADragPastTheCeilingStopsAtItAndKeepsItsShape()
    {
        var shape = Pull(VrHandle.Right, (0.5f, 0f), (10f, 0f));

        Assert.Equal(VrResize.WidestMetres, shape.WidthMetres, 3);
        Assert.True(shape.PixelsWide <= VrResize.MostPixels.Width);
        Assert.True(shape.PixelsTall <= VrResize.MostPixels.Height);
        Assert.InRange(shape.WidthMetres / shape.HeightMetres, VrResize.MostUpright, VrResize.MostLevel + 0.01f);
    }

    [Fact]
    public void ARayThatLeavesThePlaneLeavesTheShapeAsItStarted()
    {
        var grab = VrResize.Grab(Panel, VrHandle.Right, At(0.5f, 0f));
        Assert.NotNull(grab);

        var away = new VrPose(new Vector3(0.5f, 0f, 1f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI));

        Assert.Equal(Panel, VrResize.Drag(grab, away));
    }

    [Theory]
    [InlineData(0.02f, 0.5f, VrHandle.Left)]
    [InlineData(0.98f, 0.5f, VrHandle.Right)]
    [InlineData(0.5f, 0.02f, VrHandle.Top)]
    [InlineData(0.99f, 0.95f, VrHandle.Right | VrHandle.Bottom)]
    [InlineData(0.5f, 0.5f, VrHandle.None)]
    public void AHandleIsABandAlongEachEdge(float u, float v, VrHandle expected)
    {
        Assert.Equal(expected, VrResize.HandleAt(u, v, new VrExtent(1f, 2f)));
    }

    /// <summary>A small panel still has a middle a press can land on.</summary>
    [Fact]
    public void TheBandsNeverCoverTheWholeOfASmallPanel()
    {
        Assert.Equal(VrHandle.None, VrResize.HandleAt(0.5f, 0.5f, new VrExtent(VrResize.NarrowestMetres, 3f)));
    }
}
