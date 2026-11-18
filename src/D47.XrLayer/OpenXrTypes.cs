using System.Runtime.InteropServices;

namespace D47.XrLayer;

// The OpenXR 1.1 structures the layer reads and builds, from openxr.h and openxr_platform.h. Handles are 64-bit.

internal static class XrType
{
    public const int ExtensionProperties = 2;
    public const int InstanceCreateInfo = 3;
    public const int SessionCreateInfo = 8;
    public const int SwapchainCreateInfo = 9;
    public const int FrameEndInfo = 12;
    public const int EventDataSessionStateChanged = 18;
    public const int ActionStateFloat = 24;
    public const int ActionSetCreateInfo = 28;
    public const int ActionCreateInfo = 29;
    public const int InstanceProperties = 32;
    public const int CompositionLayerProjection = 35;
    public const int CompositionLayerQuad = 36;
    public const int ReferenceSpaceCreateInfo = 37;
    public const int ActionSpaceCreateInfo = 38;
    public const int SpaceLocation = 42;
    public const int InteractionProfileSuggestedBinding = 51;
    public const int SwapchainImageAcquireInfo = 55;
    public const int SwapchainImageWaitInfo = 56;
    public const int SwapchainImageReleaseInfo = 57;
    public const int ActionStateGetInfo = 58;
    public const int SessionActionSetsAttachInfo = 60;
    public const int ActionsSyncInfo = 61;
    public const int CompositionLayerCylinder = 1000017000;
    public const int GraphicsBindingVulkan = 1000025000;
    public const int GraphicsBindingD3D11 = 1000027000;
    public const int SwapchainImageD3D11 = 1000027001;
}

internal static class XrConstant
{
    public const int ReferenceSpaceView = 1;
    public const int ReferenceSpaceLocal = 2;
    public const ulong LayerBlendTextureSourceAlpha = 0x2;
    public const ulong LayerUnpremultipliedAlpha = 0x4;
    public const ulong SwapchainUsageColorAttachment = 0x1;
    public const ulong SwapchainUsageTransferDst = 0x10;
    public const ulong SwapchainUsageSampled = 0x20;
    public const ulong LocationOrientationValid = 0x1;
    public const ulong LocationPositionValid = 0x2;
    public const int SessionStateFocused = 5;
    public const int ActionTypeFloatInput = 2;
    public const int ActionTypePoseInput = 4;
    public const int MaxActionSetNameSize = 64;
    public const int MaxLocalizedActionSetNameSize = 128;
    public const int MaxActionNameSize = 64;
    public const int MaxLocalizedActionNameSize = 128;
    public const int MaxExtensionNameSize = 128;
    public const int MaxApplicationNameSize = 128;
    public const int MaxEngineNameSize = 128;

    /// <summary>XR_MIN_COMPOSITION_LAYERS_SUPPORTED: every runtime takes at least this many layers.</summary>
    public const int MinLayersSupported = 16;

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM_SRGB.</summary>
    public const long FormatBgraSrgb = 91;

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM.</summary>
    public const long FormatBgra = 87;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrBaseHeader
{
    public int Type;
    public void* Next;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrApplicationInfo
{
    public fixed byte ApplicationName[XrConstant.MaxApplicationNameSize];
    public uint ApplicationVersion;
    public fixed byte EngineName[XrConstant.MaxEngineNameSize];
    public uint EngineVersion;
    public ulong ApiVersion;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrInstanceCreateInfo
{
    public int Type;
    public void* Next;
    public ulong CreateFlags;
    public XrApplicationInfo ApplicationInfo;
    public uint EnabledApiLayerCount;
    public byte** EnabledApiLayerNames;
    public uint EnabledExtensionCount;
    public byte** EnabledExtensionNames;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrExtensionProperties
{
    public int Type;
    public void* Next;
    public fixed byte ExtensionName[XrConstant.MaxExtensionNameSize];
    public uint ExtensionVersion;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrSessionCreateInfo
{
    public int Type;
    public void* Next;
    public ulong CreateFlags;
    public ulong SystemId;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrGraphicsBindingD3D11
{
    public int Type;
    public void* Next;
    public nint Device;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrInstanceProperties
{
    public int Type;
    public void* Next;
    public ulong RuntimeVersion;
    public fixed byte RuntimeName[XrBlock.RuntimeNameLength];
}

[StructLayout(LayoutKind.Sequential)]
internal struct XrPosef
{
    public float OrientationX;
    public float OrientationY;
    public float OrientationZ;
    public float OrientationW;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrReferenceSpaceCreateInfo
{
    public int Type;
    public void* Next;
    public int ReferenceSpaceType;
    public XrPosef PoseInReferenceSpace;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrSpaceLocation
{
    public int Type;
    public void* Next;
    public ulong LocationFlags;
    public XrPosef Pose;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrSwapchainCreateInfo
{
    public int Type;
    public void* Next;
    public ulong CreateFlags;
    public ulong UsageFlags;
    public long Format;
    public uint SampleCount;
    public uint Width;
    public uint Height;
    public uint FaceCount;
    public uint ArraySize;
    public uint MipCount;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrSwapchainImageD3D11
{
    public int Type;
    public void* Next;
    public nint Texture;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrSwapchainImageWaitInfo
{
    public int Type;
    public void* Next;
    public long Timeout;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrFrameEndInfo
{
    public int Type;
    public void* Next;
    public long DisplayTime;
    public int EnvironmentBlendMode;
    public uint LayerCount;
    public XrBaseHeader** Layers;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XrRect2Di
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XrSwapchainSubImage
{
    public ulong Swapchain;
    public XrRect2Di ImageRect;
    public uint ImageArrayIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrCompositionLayerQuad
{
    public int Type;
    public void* Next;
    public ulong LayerFlags;
    public ulong Space;
    public int EyeVisibility;
    public XrSwapchainSubImage SubImage;
    public XrPosef Pose;
    public float Width;
    public float Height;
}

/// <summary>From XR_KHR_composition_layer_cylinder.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrCompositionLayerCylinderKHR
{
    public int Type;
    public void* Next;
    public ulong LayerFlags;
    public ulong Space;
    public int EyeVisibility;
    public XrSwapchainSubImage SubImage;
    public XrPosef Pose;
    public float Radius;
    public float CentralAngle;
    public float AspectRatio;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrEventDataSessionStateChanged
{
    public int Type;
    public void* Next;
    public ulong Session;
    public int State;
    public long Time;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrActionSetCreateInfo
{
    public int Type;
    public void* Next;
    public fixed byte ActionSetName[XrConstant.MaxActionSetNameSize];
    public fixed byte LocalizedActionSetName[XrConstant.MaxLocalizedActionSetNameSize];
    public uint Priority;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrActionCreateInfo
{
    public int Type;
    public void* Next;
    public fixed byte ActionName[XrConstant.MaxActionNameSize];
    public int ActionType;
    public uint CountSubactionPaths;
    public ulong* SubactionPaths;
    public fixed byte LocalizedActionName[XrConstant.MaxLocalizedActionNameSize];
}

[StructLayout(LayoutKind.Sequential)]
internal struct XrActionSuggestedBinding
{
    public ulong Action;
    public ulong Binding;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrInteractionProfileSuggestedBinding
{
    public int Type;
    public void* Next;
    public ulong InteractionProfile;
    public uint CountSuggestedBindings;
    public XrActionSuggestedBinding* SuggestedBindings;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrSessionActionSetsAttachInfo
{
    public int Type;
    public void* Next;
    public uint CountActionSets;
    public ulong* ActionSets;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XrActiveActionSet
{
    public ulong ActionSet;
    public ulong SubactionPath;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrActionsSyncInfo
{
    public int Type;
    public void* Next;
    public uint CountActiveActionSets;
    public XrActiveActionSet* ActiveActionSets;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrActionSpaceCreateInfo
{
    public int Type;
    public void* Next;
    public ulong Action;
    public ulong SubactionPath;
    public XrPosef PoseInActionSpace;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrActionStateGetInfo
{
    public int Type;
    public void* Next;
    public ulong Action;
    public ulong SubactionPath;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XrActionStateFloat
{
    public int Type;
    public void* Next;
    public float CurrentState;
    public uint ChangedSinceLastSync;
    public long LastChangeTime;
    public uint IsActive;
}

/// <summary>D3D11_TEXTURE2D_DESC.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TextureDescription
{
    public uint Width;
    public uint Height;
    public uint MipLevels;
    public uint ArraySize;
    public uint Format;
    public uint SampleCount;
    public uint SampleQuality;
    public uint Usage;
    public uint BindFlags;
    public uint CpuAccessFlags;
    public uint MiscFlags;
}
