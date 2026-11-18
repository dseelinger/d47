using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>
/// With the cylinder extension on, the panel and avatar slots are drawn as cylinders when they have a curvature:
/// radius W/(c·2π), central angle c·2π kept below 2π, aspect from the sub-image, and the pose moved the radius
/// along the slot's own +Z.
/// </summary>
public sealed class ACurvedSlotIsDrawnAsACylinderTests : IDisposable
{
    private readonly LayerRig rig = new(cylinders: true);

    [Fact]
    public void TheLayerSaysItDrawsCylinders()
    {
        Assert.Equal(1u, rig.Published.Cylinders);

        using var flat = new LayerRig();
        Assert.Equal(0u, flat.Published.Cylinders);
    }

    [Fact]
    public void ACurvedPanelBecomesACylinderOnItsOwnPlusZ()
    {
        rig.Show(0);
        ref var slot = ref rig.Slot(0);
        slot.Curvature = 0.5f;

        // Turned a quarter about Y, so the slot's own +Z is the world's +X.
        slot.Pose.OrientationY = MathF.Sqrt(0.5f);
        slot.Pose.OrientationW = MathF.Sqrt(0.5f);
        rig.Publish();
        rig.Run();

        var frame = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.Empty(frame.Quads);
        var cylinder = Assert.Single(frame.Cylinders);
        Assert.Equal(rig.EliteLayer, frame.Layers[0]);
        Assert.Equal(XrType.CompositionLayerCylinder, cylinder.Type);
        Assert.Equal(XrConstant.LayerBlendTextureSourceAlpha, cylinder.LayerFlags);
        Assert.Equal(LayerRig.LocalSpace, cylinder.Space);
        Assert.Equal(rig.Next.Swapchains.Single().Handle, cylinder.SubImage.Swapchain);
        Assert.Equal((800, 300), (cylinder.SubImage.ImageRect.Width, cylinder.SubImage.ImageRect.Height));

        var radius = 1f / MathF.PI;
        Assert.Equal(radius, cylinder.Radius, 5);
        Assert.Equal(MathF.PI, cylinder.CentralAngle, 5);
        Assert.Equal(800f / 300f, cylinder.AspectRatio, 5);
        Assert.Equal(radius, cylinder.Pose.PositionX, 5);
        Assert.Equal(0f, cylinder.Pose.PositionY, 5);
        Assert.Equal(-1f, cylinder.Pose.PositionZ, 5);
        Assert.Equal(MathF.Sqrt(0.5f), cylinder.Pose.OrientationY, 5);
        Assert.Equal(MathF.Sqrt(0.5f), cylinder.Pose.OrientationW, 5);
    }

    [Fact]
    public void AtCurvatureOneTheAngleStaysJustBelowAFullCircle()
    {
        rig.Show(1);
        rig.Slot(1).Curvature = 1f;
        rig.Publish();
        rig.Run();

        var cylinder = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single().Cylinders.Single();

        Assert.True(cylinder.CentralAngle < MathF.Tau);
        Assert.True((double)cylinder.CentralAngle < 2 * Math.PI);
        Assert.Equal(MathF.Tau, cylinder.CentralAngle, 4);
        Assert.Equal(1f / MathF.Tau, cylinder.Radius, 5);
        Assert.Equal(-1f + (1f / MathF.Tau), cylinder.Pose.PositionZ, 5);
    }

    [Theory]
    [InlineData(2, 0.5f)]
    [InlineData(0, 0.0005f)]
    [InlineData(0, 0f)]
    [InlineData(0, float.NaN)]
    [InlineData(0, 2f)]
    public void AnyOtherSlotOrCurvatureIsAQuad(int index, float curvature)
    {
        rig.Show(index);
        rig.Slot(index).Curvature = curvature;
        rig.Publish();
        rig.Run();

        var frame = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.Empty(frame.Cylinders);
        Assert.Equal((float)index, Assert.Single(frame.Quads).Pose.PositionX);
    }

    [Fact]
    public void WithoutTheExtensionACurvedPanelIsAQuad()
    {
        using var flat = new LayerRig();
        flat.Show(0);
        flat.Slot(0).Curvature = 0.5f;
        flat.Publish();
        flat.Run();

        var frame = flat.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.Empty(frame.Cylinders);
        Assert.Single(frame.Quads);
    }

    public void Dispose() => rig.Dispose();
}
