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
    public void EachActionStructHasItsHeaderSize()
    {
        Assert.Equal(40, sizeof(XrEventDataSessionStateChanged));
        Assert.Equal(216, sizeof(XrActionSetCreateInfo));
        Assert.Equal(224, sizeof(XrActionCreateInfo));
        Assert.Equal(16, sizeof(XrActionSuggestedBinding));
        Assert.Equal(40, sizeof(XrInteractionProfileSuggestedBinding));
        Assert.Equal(32, sizeof(XrSessionActionSetsAttachInfo));
        Assert.Equal(16, sizeof(XrActiveActionSet));
        Assert.Equal(32, sizeof(XrActionsSyncInfo));
        Assert.Equal(64, sizeof(XrActionSpaceCreateInfo));
        Assert.Equal(32, sizeof(XrActionStateGetInfo));
        Assert.Equal(40, sizeof(XrActionStateFloat));
    }

    [Fact]
    public void TheActionFieldsSitWhereTheRuntimeReadsThem()
    {
        var setInfo = default(XrActionSetCreateInfo);
        var s = (byte*)&setInfo;
        Assert.Equal(80, setInfo.LocalizedActionSetName - s);
        Assert.Equal(208, (byte*)&setInfo.Priority - s);

        var actionInfo = default(XrActionCreateInfo);
        var a = (byte*)&actionInfo;
        Assert.Equal(80, (byte*)&actionInfo.ActionType - a);
        Assert.Equal(88, (byte*)&actionInfo.SubactionPaths - a);
        Assert.Equal(96, actionInfo.LocalizedActionName - a);

        var state = default(XrActionStateFloat);
        var f = (byte*)&state;
        Assert.Equal(16, (byte*)&state.CurrentState - f);
        Assert.Equal(24, (byte*)&state.LastChangeTime - f);
        Assert.Equal(32, (byte*)&state.IsActive - f);

        var changed = default(XrEventDataSessionStateChanged);
        var e = (byte*)&changed;
        Assert.Equal(16, (byte*)&changed.Session - e);
        Assert.Equal(24, (byte*)&changed.State - e);
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
