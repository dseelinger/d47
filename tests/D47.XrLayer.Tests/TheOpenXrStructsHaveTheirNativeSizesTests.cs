using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>Sizes from openxr.h, openxr_platform.h and d3d11.h on x64.</summary>
public sealed unsafe class TheOpenXrStructsHaveTheirNativeSizesTests
{
    [Fact]
    public void EachStructHasItsHeaderSize()
    {
        Assert.Equal(16, sizeof(XrBaseHeader));
        Assert.Equal(32, sizeof(XrSessionCreateInfo));
        Assert.Equal(24, sizeof(XrGraphicsBindingD3D11));
        Assert.Equal(152, sizeof(XrInstanceProperties));
        Assert.Equal(28, sizeof(XrPosef));
        Assert.Equal(48, sizeof(XrReferenceSpaceCreateInfo));
        Assert.Equal(56, sizeof(XrSpaceLocation));
        Assert.Equal(64, sizeof(XrSwapchainCreateInfo));
        Assert.Equal(24, sizeof(XrSwapchainImageD3D11));
        Assert.Equal(24, sizeof(XrSwapchainImageWaitInfo));
        Assert.Equal(40, sizeof(XrFrameEndInfo));
        Assert.Equal(32, sizeof(XrSwapchainSubImage));
        Assert.Equal(112, sizeof(XrCompositionLayerQuad));
        Assert.Equal(44, sizeof(TextureDescription));
    }

    [Fact]
    public void TheQuadsFieldsSitWhereTheRuntimeReadsThem()
    {
        var quad = default(XrCompositionLayerQuad);
        var q = (byte*)&quad;
        Assert.Equal(16, (byte*)&quad.LayerFlags - q);
        Assert.Equal(24, (byte*)&quad.Space - q);
        Assert.Equal(32, (byte*)&quad.EyeVisibility - q);
        Assert.Equal(40, (byte*)&quad.SubImage - q);
        Assert.Equal(72, (byte*)&quad.Pose - q);
        Assert.Equal(100, (byte*)&quad.Width - q);
    }
}
