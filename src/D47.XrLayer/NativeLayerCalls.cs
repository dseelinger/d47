using System.Runtime.InteropServices;

namespace D47.XrLayer;

/// <summary>The next layer's functions for one instance, found through its xrGetInstanceProcAddr.</summary>
internal sealed unsafe class NativeLayerCalls : ILayerCalls
{
    private readonly delegate* unmanaged<ulong, XrInstanceProperties*, int> getInstanceProperties;
    private readonly delegate* unmanaged<ulong, uint, uint*, long*, int> enumerateSwapchainFormats;
    private readonly delegate* unmanaged<ulong, XrReferenceSpaceCreateInfo*, ulong*, int> createReferenceSpace;
    private readonly delegate* unmanaged<ulong, int> destroySpace;
    private readonly delegate* unmanaged<ulong, ulong, long, XrSpaceLocation*, int> locateSpace;
    private readonly delegate* unmanaged<ulong, XrSwapchainCreateInfo*, ulong*, int> createSwapchain;
    private readonly delegate* unmanaged<ulong, int> destroySwapchain;
    private readonly delegate* unmanaged<ulong, uint, uint*, XrSwapchainImageD3D11*, int> enumerateSwapchainImages;
    private readonly delegate* unmanaged<ulong, XrBaseHeader*, uint*, int> acquireSwapchainImage;
    private readonly delegate* unmanaged<ulong, XrSwapchainImageWaitInfo*, int> waitSwapchainImage;
    private readonly delegate* unmanaged<ulong, XrBaseHeader*, int> releaseSwapchainImage;
    private readonly delegate* unmanaged<ulong, XrFrameEndInfo*, int> endFrame;

    private NativeLayerCalls(delegate* unmanaged<ulong, byte*, nint*, int> next, ulong instance)
    {
        getInstanceProperties = (delegate* unmanaged<ulong, XrInstanceProperties*, int>)Find(next, instance, "xrGetInstanceProperties"u8);
        enumerateSwapchainFormats = (delegate* unmanaged<ulong, uint, uint*, long*, int>)Find(next, instance, "xrEnumerateSwapchainFormats"u8);
        createReferenceSpace = (delegate* unmanaged<ulong, XrReferenceSpaceCreateInfo*, ulong*, int>)Find(next, instance, "xrCreateReferenceSpace"u8);
        destroySpace = (delegate* unmanaged<ulong, int>)Find(next, instance, "xrDestroySpace"u8);
        locateSpace = (delegate* unmanaged<ulong, ulong, long, XrSpaceLocation*, int>)Find(next, instance, "xrLocateSpace"u8);
        createSwapchain = (delegate* unmanaged<ulong, XrSwapchainCreateInfo*, ulong*, int>)Find(next, instance, "xrCreateSwapchain"u8);
        destroySwapchain = (delegate* unmanaged<ulong, int>)Find(next, instance, "xrDestroySwapchain"u8);
        enumerateSwapchainImages = (delegate* unmanaged<ulong, uint, uint*, XrSwapchainImageD3D11*, int>)Find(next, instance, "xrEnumerateSwapchainImages"u8);
        acquireSwapchainImage = (delegate* unmanaged<ulong, XrBaseHeader*, uint*, int>)Find(next, instance, "xrAcquireSwapchainImage"u8);
        waitSwapchainImage = (delegate* unmanaged<ulong, XrSwapchainImageWaitInfo*, int>)Find(next, instance, "xrWaitSwapchainImage"u8);
        releaseSwapchainImage = (delegate* unmanaged<ulong, XrBaseHeader*, int>)Find(next, instance, "xrReleaseSwapchainImage"u8);
        endFrame = (delegate* unmanaged<ulong, XrFrameEndInfo*, int>)Find(next, instance, "xrEndFrame"u8);
    }

    /// <summary>The next layer's functions, or null when any of them is missing.</summary>
    public static NativeLayerCalls? Resolve(delegate* unmanaged<ulong, byte*, nint*, int> next, ulong instance)
    {
        if (next is null)
        {
            return null;
        }

        var calls = new NativeLayerCalls(next, instance);
        return calls.getInstanceProperties is null || calls.enumerateSwapchainFormats is null
            || calls.createReferenceSpace is null || calls.destroySpace is null || calls.locateSpace is null
            || calls.createSwapchain is null || calls.destroySwapchain is null
            || calls.enumerateSwapchainImages is null || calls.acquireSwapchainImage is null
            || calls.waitSwapchainImage is null || calls.releaseSwapchainImage is null || calls.endFrame is null
            ? null
            : calls;
    }

    public int GetInstanceProperties(ulong instance, XrInstanceProperties* properties) =>
        getInstanceProperties(instance, properties);

    public int EnumerateSwapchainFormats(ulong session, uint capacity, uint* count, long* formats) =>
        enumerateSwapchainFormats(session, capacity, count, formats);

    public int CreateReferenceSpace(ulong session, XrReferenceSpaceCreateInfo* createInfo, ulong* space) =>
        createReferenceSpace(session, createInfo, space);

    public int DestroySpace(ulong space) => destroySpace(space);

    public int LocateSpace(ulong space, ulong baseSpace, long time, XrSpaceLocation* location) =>
        locateSpace(space, baseSpace, time, location);

    public int CreateSwapchain(ulong session, XrSwapchainCreateInfo* createInfo, ulong* swapchain) =>
        createSwapchain(session, createInfo, swapchain);

    public int DestroySwapchain(ulong swapchain) => destroySwapchain(swapchain);

    public int EnumerateSwapchainImages(ulong swapchain, uint capacity, uint* count, XrSwapchainImageD3D11* images) =>
        enumerateSwapchainImages(swapchain, capacity, count, images);

    public int AcquireSwapchainImage(ulong swapchain, uint* index)
    {
        var info = new XrBaseHeader { Type = XrType.SwapchainImageAcquireInfo };
        return acquireSwapchainImage(swapchain, &info, index);
    }

    public int WaitSwapchainImage(ulong swapchain, long timeoutNanoseconds)
    {
        var info = new XrSwapchainImageWaitInfo { Type = XrType.SwapchainImageWaitInfo, Timeout = timeoutNanoseconds };
        return waitSwapchainImage(swapchain, &info);
    }

    public int ReleaseSwapchainImage(ulong swapchain)
    {
        var info = new XrBaseHeader { Type = XrType.SwapchainImageReleaseInfo };
        return releaseSwapchainImage(swapchain, &info);
    }

    public int EndFrame(ulong session, XrFrameEndInfo* frameEndInfo) => endFrame(session, frameEndInfo);

    public ITextureDevice? OpenDevice(nint d3d11Device) => D3D11TextureDevice.Open(d3d11Device);

    private static nint Find(delegate* unmanaged<ulong, byte*, nint*, int> next, ulong instance, ReadOnlySpan<byte> name)
    {
        nint function = 0;
        fixed (byte* text = name)
        {
            return next(instance, text, &function) < XrResult.Success ? 0 : function;
        }
    }
}

/// <summary>Opens the shared block once for the process.</summary>
internal static unsafe class SharedBlock
{
    private const uint PageReadWrite = 0x04;
    private const uint FileMapAllAccess = 0xF001F;

    /// <summary>Creates or opens the block. Returns null when it cannot be mapped.</summary>
    public static XrBlock* Map()
    {
        nint mapping;
        fixed (char* name = XrBlock.Name)
        {
            mapping = CreateFileMappingW(-1, 0, PageReadWrite, 0, (uint)sizeof(XrBlock), name);
        }

        if (mapping == 0)
        {
            return null;
        }

        var view = MapViewOfFile(mapping, FileMapAllAccess, 0, 0, (nuint)sizeof(XrBlock));
        return (XrBlock*)view;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint CreateFileMappingW(nint file, nint attributes, uint protect, uint sizeHigh, uint sizeLow, char* name);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void* MapViewOfFile(nint mapping, uint access, uint offsetHigh, uint offsetLow, nuint bytes);
}
