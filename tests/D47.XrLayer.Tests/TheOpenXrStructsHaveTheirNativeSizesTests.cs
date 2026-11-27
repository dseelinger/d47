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
        Assert.Equal(112, sizeof(XrCompositionLayerCylinderKHR));
        Assert.Equal(16, sizeof(XrFovf));
        Assert.Equal(96, sizeof(XrCompositionLayerProjectionView));
        Assert.Equal(48, sizeof(XrCompositionLayerProjection));
        Assert.Equal(44, sizeof(TextureDescription));
    }

    [Fact]
    public void TheInstanceStructsHaveTheirHeaderSizes()
    {
        Assert.Equal(272, sizeof(XrApplicationInfo));
        Assert.Equal(328, sizeof(XrInstanceCreateInfo));
        Assert.Equal(152, sizeof(XrExtensionProperties));

        var createInfo = default(XrInstanceCreateInfo);
        var c = (byte*)&createInfo;
        Assert.Equal(24, (byte*)&createInfo.ApplicationInfo - c);
        Assert.Equal(296, (byte*)&createInfo.EnabledApiLayerCount - c);
        Assert.Equal(312, (byte*)&createInfo.EnabledExtensionCount - c);
        Assert.Equal(320, (byte*)&createInfo.EnabledExtensionNames - c);

        var properties = default(XrExtensionProperties);
        var p = (byte*)&properties;
        Assert.Equal(16, properties.ExtensionName - p);
        Assert.Equal(144, (byte*)&properties.ExtensionVersion - p);
    }

    [Fact]
    public void TheCylindersFieldsSitWhereTheRuntimeReadsThem()
    {
        var cylinder = default(XrCompositionLayerCylinderKHR);
        var c = (byte*)&cylinder;
        Assert.Equal(16, (byte*)&cylinder.LayerFlags - c);
        Assert.Equal(24, (byte*)&cylinder.Space - c);
        Assert.Equal(32, (byte*)&cylinder.EyeVisibility - c);
        Assert.Equal(40, (byte*)&cylinder.SubImage - c);
        Assert.Equal(72, (byte*)&cylinder.Pose - c);
        Assert.Equal(100, (byte*)&cylinder.Radius - c);
        Assert.Equal(104, (byte*)&cylinder.CentralAngle - c);
        Assert.Equal(108, (byte*)&cylinder.AspectRatio - c);
    }

    [Fact]
    public void TheProjectionFieldsSitWhereEliteWritesThem()
    {
        var projection = default(XrCompositionLayerProjection);
        var p = (byte*)&projection;
        Assert.Equal(16, (byte*)&projection.LayerFlags - p);
        Assert.Equal(24, (byte*)&projection.Space - p);
        Assert.Equal(32, (byte*)&projection.ViewCount - p);
        Assert.Equal(40, (byte*)&projection.Views - p);

        var view = default(XrCompositionLayerProjectionView);
        var v = (byte*)&view;
        Assert.Equal(16, (byte*)&view.Pose - v);
        Assert.Equal(44, (byte*)&view.Fov - v);
        Assert.Equal(64, (byte*)&view.SubImage - v);
        Assert.Equal(72, (byte*)&view.SubImage.ImageRect - v);
        Assert.Equal(88, (byte*)&view.SubImage.ImageArrayIndex - v);
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
        Assert.Equal(40, sizeof(XrActionStateBoolean));
        Assert.Equal(48, sizeof(XrActionStateVector2f));
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

        var held = default(XrActionStateBoolean);
        var h = (byte*)&held;
        Assert.Equal(16, (byte*)&held.CurrentState - h);
        Assert.Equal(20, (byte*)&held.ChangedSinceLastSync - h);
        Assert.Equal(24, (byte*)&held.LastChangeTime - h);
        Assert.Equal(32, (byte*)&held.IsActive - h);

        var stick = default(XrActionStateVector2f);
        var v = (byte*)&stick;
        Assert.Equal(16, (byte*)&stick.CurrentStateX - v);
        Assert.Equal(20, (byte*)&stick.CurrentStateY - v);
        Assert.Equal(24, (byte*)&stick.ChangedSinceLastSync - v);
        Assert.Equal(32, (byte*)&stick.LastChangeTime - v);
        Assert.Equal(40, (byte*)&stick.IsActive - v);

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
