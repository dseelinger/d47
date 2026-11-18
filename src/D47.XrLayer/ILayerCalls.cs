namespace D47.XrLayer;

/// <summary>The next layer's OpenXR functions an Elite session calls, and the Direct3D 11 device behind it.</summary>
internal unsafe interface ILayerCalls
{
    int GetInstanceProperties(ulong instance, XrInstanceProperties* properties);

    int EnumerateSwapchainFormats(ulong session, uint capacity, uint* count, long* formats);

    int CreateReferenceSpace(ulong session, XrReferenceSpaceCreateInfo* createInfo, ulong* space);

    int DestroySpace(ulong space);

    int LocateSpace(ulong space, ulong baseSpace, long time, XrSpaceLocation* location);

    int CreateSwapchain(ulong session, XrSwapchainCreateInfo* createInfo, ulong* swapchain);

    int DestroySwapchain(ulong swapchain);

    int EnumerateSwapchainImages(ulong swapchain, uint capacity, uint* count, XrSwapchainImageD3D11* images);

    int AcquireSwapchainImage(ulong swapchain, uint* index);

    int WaitSwapchainImage(ulong swapchain, long timeoutNanoseconds);

    int ReleaseSwapchainImage(ulong swapchain);

    int EndFrame(ulong session, XrFrameEndInfo* frameEndInfo);

    int StringToPath(ulong instance, byte* pathString, ulong* path);

    int CreateActionSet(ulong instance, XrActionSetCreateInfo* createInfo, ulong* actionSet);

    int DestroyActionSet(ulong actionSet);

    int CreateAction(ulong actionSet, XrActionCreateInfo* createInfo, ulong* action);

    int SuggestInteractionProfileBindings(ulong instance, XrInteractionProfileSuggestedBinding* suggestedBindings);

    int AttachSessionActionSets(ulong session, XrSessionActionSetsAttachInfo* attachInfo);

    int SyncActions(ulong session, XrActionsSyncInfo* syncInfo);

    int CreateActionSpace(ulong session, XrActionSpaceCreateInfo* createInfo, ulong* space);

    int GetActionStateFloat(ulong session, XrActionStateGetInfo* getInfo, XrActionStateFloat* state);

    int GetActionStateBoolean(ulong session, XrActionStateGetInfo* getInfo, XrActionStateBoolean* state);

    int GetActionStateVector2f(ulong session, XrActionStateGetInfo* getInfo, XrActionStateVector2f* state);

    /// <summary>The session's Direct3D 11 device, or null when it cannot be used.</summary>
    ITextureDevice? OpenDevice(nint d3d11Device);
}

/// <summary>Shared textures on the session's Direct3D 11 device.</summary>
internal unsafe interface ITextureDevice
{
    /// <summary>The LUID of the device's adapter, as its low and high parts.</summary>
    (uint Low, int High) AdapterLuid { get; }

    /// <summary>Opens a shared texture by its null-terminated name for reading. Returns 0 when it cannot.</summary>
    nint OpenShared(char* name);

    TextureDescription Describe(nint texture);

    /// <summary>Copies the whole of <paramref name="source"/> into <paramref name="destination"/> on the immediate context.</summary>
    void Copy(nint source, nint destination);

    /// <summary>Releases a texture <see cref="OpenShared"/> returned.</summary>
    void Release(nint texture);

    /// <summary>Releases the device's own references.</summary>
    void Close();
}
