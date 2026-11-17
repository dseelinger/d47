namespace D47.XrLayer;

/// <summary>
/// The layer's work in one Elite session: appends d47's quads after Elite's layers, reads the motion controllers and
/// publishes the layer's half of the block. <see cref="EndFrame"/> never throws and never waits on d47.
/// </summary>
internal sealed unsafe class EliteSession
{
    /// <summary>How long d47's heartbeat may stand still before its quads are removed, in nanoseconds of display time.</summary>
    public const long HeartbeatTimeout = 1_000_000_000;

    /// <summary>The longest <see cref="EndFrame"/> waits for a swapchain image, in nanoseconds.</summary>
    public const long SwapchainWait = 1_000_000;

    private const int MostSwapchainImages = 16;
    private const int MostSwapchainFormats = 256;
    private const int MostNextLinks = 64;

    private readonly ILayerCalls calls;
    private readonly XrBlock* block;
    private readonly Slot[] slots = new Slot[XrBlock.SlotCount];
    private readonly Controllers controllers;
    private ITextureDevice? device;
    private ulong localSpace;
    private ulong viewSpace;
    private long swapchainFormat;
    private bool drawing;
    private XrLayerHalf published;
    private XrD47Half lastD47;
    private bool heartbeatSeen;
    private bool heartbeatAdvanced;
    private ulong lastHeartbeat;
    private long lastAdvance;
    private long textureUses;

    private EliteSession(ILayerCalls calls, XrBlock* block, SuggestedBindings suggested, ulong instance, ulong handle)
    {
        this.calls = calls;
        this.block = block;
        Handle = handle;
        controllers = new Controllers(calls, suggested, instance, handle);
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new Slot();
        }
    }

    public ulong Handle { get; }

    /// <summary>Starts the layer's work in a session Elite has just created. <paramref name="block"/> may be null.</summary>
    public static EliteSession Attach(
        ILayerCalls calls, XrBlock* block, SuggestedBindings suggested, ulong instance, XrSessionCreateInfo* createInfo, ulong handle)
    {
        var session = new EliteSession(calls, block, suggested, instance, handle);
        try
        {
            session.Start(instance, createInfo);
        }
        catch (Exception)
        {
            session.TurnOff(XrRefusal.Faulted);
        }

        return session;
    }

    /// <summary>Ends Elite's frame through the next layer, with d47's quads appended while they can be drawn.</summary>
    public int EndFrame(XrFrameEndInfo* frameEndInfo)
    {
        var quads = stackalloc XrCompositionLayerQuad[XrBlock.SlotCount];
        var layers = stackalloc nint[XrConstant.MinLayersSupported];
        XrFrameEndInfo withQuads = default;
        int added;
        try
        {
            added = Prepare(frameEndInfo, quads, layers, &withQuads);
        }
        catch (Exception)
        {
            TurnOff(XrRefusal.Faulted);
            added = 0;
        }

        if (added == 0)
        {
            return calls.EndFrame(Handle, frameEndInfo);
        }

        var result = calls.EndFrame(Handle, &withQuads);
        if (result >= XrResult.Success)
        {
            return result;
        }

        TurnOff(XrRefusal.FrameRejected);
        return calls.EndFrame(Handle, frameEndInfo);
    }

    /// <summary>Elite's xrAttachSessionActionSets, passed on through <see cref="Controllers.EliteAttach"/>.</summary>
    public int AttachActionSets(XrSessionActionSetsAttachInfo* attachInfo) => controllers.EliteAttach(attachInfo, ReadPointing());

    /// <summary>Elite's xrSyncActions, passed on through <see cref="Controllers.EliteSync"/>.</summary>
    public int SyncActions(XrActionsSyncInfo* syncInfo) => controllers.EliteSync(syncInfo);

    /// <summary>Notes a state change xrPollEvent returned for this session.</summary>
    public void StateChanged(int sessionState) => controllers.StateChanged(sessionState);

    /// <summary>Releases the session's textures, swapchains, spaces, action set and device, and marks the block detached. Called before the session is destroyed.</summary>
    public void Destroy()
    {
        drawing = false;
        try
        {
            controllers.Destroy();
            ReleaseTextures();
            foreach (var slot in slots)
            {
                DestroySwapchain(slot);
            }

            if (viewSpace != 0)
            {
                calls.DestroySpace(viewSpace);
                viewSpace = 0;
            }

            if (localSpace != 0)
            {
                calls.DestroySpace(localSpace);
                localSpace = 0;
            }

            device?.Close();
            device = null;
        }
        catch (Exception)
        {
            // The session is going away either way.
        }

        published.Attached = 0;
        published.Hands = default;
        TryPublish();
    }

    private void Start(ulong instance, XrSessionCreateInfo* createInfo)
    {
        if (block is null)
        {
            return;
        }

        block->Magic = XrBlock.MagicValue;
        block->LayoutVersion = XrBlock.CurrentLayoutVersion;
        published.Attached = 1;
        ReadRuntimeName(instance);

        var binding = FindD3D11Binding(createInfo);
        if (binding is null)
        {
            Refuse(XrRefusal.NotDirect3D11);
            return;
        }

        device = calls.OpenDevice(binding->Device);
        if (device is null)
        {
            Refuse(XrRefusal.SetupFailed);
            return;
        }

        var luid = device.AdapterLuid;
        published.AdapterLuidLow = luid.Low;
        published.AdapterLuidHigh = luid.High;

        swapchainFormat = ChooseSwapchainFormat();
        if (swapchainFormat == 0)
        {
            Refuse(XrRefusal.NoSwapchainFormat);
            return;
        }

        localSpace = CreateReferenceSpace(XrConstant.ReferenceSpaceLocal);
        viewSpace = CreateReferenceSpace(XrConstant.ReferenceSpaceView);
        if (localSpace == 0 || viewSpace == 0)
        {
            Refuse(XrRefusal.SetupFailed);
            return;
        }

        controllers.LocalSpace = localSpace;
        drawing = true;
        Publish();
    }

    private int Prepare(XrFrameEndInfo* frameEndInfo, XrCompositionLayerQuad* quads, nint* layers, XrFrameEndInfo* withQuads)
    {
        if (block is null)
        {
            return 0;
        }

        published.FrameCounter++;
        if (frameEndInfo is null)
        {
            Publish();
            return 0;
        }

        if (XrSeqLock.TryRead(&block->D47, out var d47))
        {
            lastD47 = d47;
        }

        var time = frameEndInfo->DisplayTime;
        if (drawing)
        {
            LocateHead(time);
        }

        controllers.Frame(time, lastD47.Pointing == 1, ref published);
        Publish();
        if (!drawing)
        {
            return 0;
        }

        if (!HeartbeatIsMoving(time))
        {
            ReleaseTextures();
            return 0;
        }

        var eliteCount = frameEndInfo->LayerCount;
        if (eliteCount >= XrConstant.MinLayersSupported || (eliteCount > 0 && frameEndInfo->Layers is null))
        {
            return 0;
        }

        var room = XrConstant.MinLayersSupported - (int)eliteCount;
        var added = 0;
        for (var i = 0; i < XrBlock.SlotCount && added < room; i++)
        {
            if (TryQuad(slots[i], ref lastD47.Slots[i], &quads[added]))
            {
                added++;
            }
        }

        if (added == 0)
        {
            return 0;
        }

        for (var i = 0; i < eliteCount; i++)
        {
            layers[i] = (nint)frameEndInfo->Layers[i];
        }

        for (var i = 0; i < added; i++)
        {
            layers[eliteCount + i] = (nint)(&quads[i]);
        }

        *withQuads = *frameEndInfo;
        withQuads->LayerCount = eliteCount + (uint)added;
        withQuads->Layers = (XrBaseHeader**)layers;
        return added;
    }

    private bool HeartbeatIsMoving(long time)
    {
        var heartbeat = lastD47.Heartbeat;
        if (!heartbeatSeen)
        {
            heartbeatSeen = true;
            lastHeartbeat = heartbeat;
            return false;
        }

        if (heartbeat != lastHeartbeat)
        {
            lastHeartbeat = heartbeat;
            lastAdvance = time;
            heartbeatAdvanced = true;
        }

        return heartbeatAdvanced && time - lastAdvance < HeartbeatTimeout;
    }

    private bool TryQuad(Slot state, ref XrQuadSlot slot, XrCompositionLayerQuad* quad)
    {
        if (slot.Visible != 1 || !IsDrawable(in slot))
        {
            return false;
        }

        if (!EnsureSwapchain(state, slot.TextureWidth, slot.TextureHeight))
        {
            return false;
        }

        if (slot.TextureGeneration != state.CopiedGeneration)
        {
            if (slot.TextureGeneration == state.FailedGeneration)
            {
                return false;
            }

            if (CopyIn(state, ref slot) == CopyOutcome.Failed)
            {
                state.FailedGeneration = slot.TextureGeneration;
                return false;
            }
        }

        if (!state.HasPicture)
        {
            return false;
        }

        var pose = slot.Pose;
        var length = MathF.Sqrt(
            (pose.OrientationX * pose.OrientationX) + (pose.OrientationY * pose.OrientationY)
            + (pose.OrientationZ * pose.OrientationZ) + (pose.OrientationW * pose.OrientationW));

        *quad = default;
        quad->Type = XrType.CompositionLayerQuad;
        quad->LayerFlags = XrConstant.LayerBlendTextureSourceAlpha;
        quad->Space = slot.Space == XrQuadSpace.Head ? viewSpace : localSpace;
        quad->SubImage.Swapchain = state.Swapchain;
        quad->SubImage.ImageRect = new XrRect2Di
        {
            X = slot.RectX,
            Y = slot.RectY,
            Width = slot.RectWidth,
            Height = slot.RectHeight,
        };
        quad->Pose = new XrPosef
        {
            OrientationX = pose.OrientationX / length,
            OrientationY = pose.OrientationY / length,
            OrientationZ = pose.OrientationZ / length,
            OrientationW = pose.OrientationW / length,
            PositionX = pose.PositionX,
            PositionY = pose.PositionY,
            PositionZ = pose.PositionZ,
        };
        quad->Width = slot.WidthMetres;
        quad->Height = QuadHeight(in slot);
        return true;
    }

    /// <summary>True when every field of a visible slot is finite, in range and consistent.</summary>
    internal static bool IsDrawable(in XrQuadSlot slot)
    {
        if (slot.Space is not (XrQuadSpace.Head or XrQuadSpace.Cockpit) || slot.TextureGeneration == 0)
        {
            return false;
        }

        var pose = slot.Pose;
        if (!WithinReach(pose.PositionX) || !WithinReach(pose.PositionY) || !WithinReach(pose.PositionZ))
        {
            return false;
        }

        var lengthSquared = (pose.OrientationX * pose.OrientationX) + (pose.OrientationY * pose.OrientationY)
            + (pose.OrientationZ * pose.OrientationZ) + (pose.OrientationW * pose.OrientationW);
        if (!float.IsFinite(lengthSquared) || lengthSquared < 0.98f || lengthSquared > 1.02f)
        {
            return false;
        }

        if (!(slot.WidthMetres > 0f && slot.WidthMetres <= XrBlock.MaxWidthMetres))
        {
            return false;
        }

        if (slot.TextureWidth is 0 or > XrBlock.MaxTextureSide || slot.TextureHeight is 0 or > XrBlock.MaxTextureSide)
        {
            return false;
        }

        if (slot.RectX < 0 || slot.RectY < 0 || slot.RectWidth <= 0 || slot.RectHeight <= 0
            || (long)slot.RectX + slot.RectWidth > slot.TextureWidth
            || (long)slot.RectY + slot.RectHeight > slot.TextureHeight)
        {
            return false;
        }

        var height = QuadHeight(in slot);
        if (!(height > 0f && height <= XrBlock.MaxWidthMetres))
        {
            return false;
        }

        return NameLength(in slot.TextureName) > 0;
    }

    private static bool WithinReach(float value) => float.IsFinite(value) && MathF.Abs(value) <= XrBlock.MaxDistanceMetres;

    private static float QuadHeight(in XrQuadSlot slot) => slot.WidthMetres * slot.RectHeight / slot.RectWidth;

    /// <summary>The characters before the terminator, or 0 when there is no terminator.</summary>
    private static int NameLength(in XrTextureName name)
    {
        ReadOnlySpan<char> characters = name;
        var end = characters.IndexOf('\0');
        return end < 0 ? 0 : end;
    }

    private bool EnsureSwapchain(Slot state, uint width, uint height)
    {
        if (state.Width == width && state.Height == height)
        {
            return state.Swapchain != 0;
        }

        DestroySwapchain(state);
        state.Width = width;
        state.Height = height;

        var createInfo = new XrSwapchainCreateInfo
        {
            Type = XrType.SwapchainCreateInfo,
            UsageFlags = XrConstant.SwapchainUsageColorAttachment | XrConstant.SwapchainUsageTransferDst
                | XrConstant.SwapchainUsageSampled,
            Format = swapchainFormat,
            SampleCount = 1,
            Width = width,
            Height = height,
            FaceCount = 1,
            ArraySize = 1,
            MipCount = 1,
        };
        ulong swapchain = 0;
        if (calls.CreateSwapchain(Handle, &createInfo, &swapchain) < XrResult.Success || swapchain == 0)
        {
            return false;
        }

        state.Swapchain = swapchain;
        uint count = 0;
        if (calls.EnumerateSwapchainImages(swapchain, 0, &count, null) < XrResult.Success
            || count is 0 or > MostSwapchainImages)
        {
            DestroySwapchain(state);
            return false;
        }

        var images = stackalloc XrSwapchainImageD3D11[(int)count];
        for (var i = 0; i < count; i++)
        {
            images[i] = new XrSwapchainImageD3D11 { Type = XrType.SwapchainImageD3D11 };
        }

        if (calls.EnumerateSwapchainImages(swapchain, count, &count, images) < XrResult.Success)
        {
            DestroySwapchain(state);
            return false;
        }

        state.Images = new nint[count];
        for (var i = 0; i < count; i++)
        {
            state.Images[i] = images[i].Texture;
        }

        return true;
    }

    private void DestroySwapchain(Slot state)
    {
        if (state.Swapchain != 0)
        {
            calls.DestroySwapchain(state.Swapchain);
        }

        state.Swapchain = 0;
        state.Images = [];
        state.Acquired = false;
        state.HasPicture = false;
        state.CopiedGeneration = 0;
        state.FailedGeneration = 0;
    }

    private CopyOutcome CopyIn(Slot state, ref XrQuadSlot slot)
    {
        var texture = OpenTexture(state, ref slot);
        if (texture == 0)
        {
            return CopyOutcome.Failed;
        }

        if (!state.Acquired)
        {
            uint index = 0;
            if (calls.AcquireSwapchainImage(state.Swapchain, &index) < XrResult.Success)
            {
                return CopyOutcome.Failed;
            }

            state.Acquired = true;
            state.AcquiredIndex = index;
        }

        var waited = calls.WaitSwapchainImage(state.Swapchain, SwapchainWait);
        if (waited == XrResult.TimeoutExpired)
        {
            return CopyOutcome.Pending;
        }

        if (waited < XrResult.Success)
        {
            return CopyOutcome.Failed;
        }

        var copied = false;
        if (state.AcquiredIndex < state.Images.Length && state.Images[state.AcquiredIndex] != 0)
        {
            device!.Copy(texture, state.Images[state.AcquiredIndex]);
            copied = true;
        }

        state.Acquired = false;
        if (calls.ReleaseSwapchainImage(state.Swapchain) < XrResult.Success || !copied)
        {
            return CopyOutcome.Failed;
        }

        state.HasPicture = true;
        state.CopiedGeneration = slot.TextureGeneration;
        return CopyOutcome.Copied;
    }

    /// <summary>The slot's texture, opened on first use and kept while it matches the slot. 0 when it cannot be used.</summary>
    private nint OpenTexture(Slot state, ref XrQuadSlot slot)
    {
        ReadOnlySpan<char> fullName = slot.TextureName;
        var name = fullName[..NameLength(in slot.TextureName)];

        Texture? spare = null;
        foreach (var texture in state.Textures)
        {
            if (texture.Handle != 0 && name.SequenceEqual(texture.Name.AsSpan(0, texture.NameLength)))
            {
                if (texture.Width == slot.TextureWidth && texture.Height == slot.TextureHeight)
                {
                    texture.LastUse = ++textureUses;
                    return texture.Handle;
                }

                Release(texture);
                spare = texture;
                break;
            }

            if (spare is null || texture.Handle == 0 || (spare.Handle != 0 && texture.LastUse < spare.LastUse))
            {
                spare = texture;
            }
        }

        Release(spare!);
        name.CopyTo(spare!.Name);
        spare.Name[name.Length] = '\0';
        spare.NameLength = name.Length;

        nint opened;
        fixed (char* terminated = spare.Name)
        {
            opened = device!.OpenShared(terminated);
        }

        if (opened == 0)
        {
            return 0;
        }

        var description = device.Describe(opened);
        if (description.Width != slot.TextureWidth || description.Height != slot.TextureHeight
            || description.Format != XrBlock.TextureFormat || description.MipLevels != 1
            || description.ArraySize != 1 || description.SampleCount != 1)
        {
            device.Release(opened);
            return 0;
        }

        spare.Handle = opened;
        spare.Width = description.Width;
        spare.Height = description.Height;
        spare.LastUse = ++textureUses;
        return opened;
    }

    private void Release(Texture texture)
    {
        if (texture.Handle != 0)
        {
            device?.Release(texture.Handle);
        }

        texture.Handle = 0;
        texture.NameLength = 0;
    }

    private void ReleaseTextures()
    {
        foreach (var slot in slots)
        {
            foreach (var texture in slot.Textures)
            {
                Release(texture);
            }

            slot.HasPicture = false;
            slot.CopiedGeneration = 0;
            slot.FailedGeneration = 0;
        }
    }

    private void LocateHead(long time)
    {
        var location = new XrSpaceLocation { Type = XrType.SpaceLocation };
        if (calls.LocateSpace(viewSpace, localSpace, time, &location) < XrResult.Success)
        {
            published.HeadValid = 0;
            return;
        }

        published.HeadValid = (uint)(location.LocationFlags & 0xF);
        var pose = location.Pose;
        published.Head = new XrBlockPose
        {
            OrientationX = pose.OrientationX,
            OrientationY = pose.OrientationY,
            OrientationZ = pose.OrientationZ,
            OrientationW = pose.OrientationW,
            PositionX = pose.PositionX,
            PositionY = pose.PositionY,
            PositionZ = pose.PositionZ,
        };
    }

    private void ReadRuntimeName(ulong instance)
    {
        var properties = new XrInstanceProperties { Type = XrType.InstanceProperties };
        if (calls.GetInstanceProperties(instance, &properties) < XrResult.Success)
        {
            return;
        }

        Span<byte> target = published.RuntimeName;
        new ReadOnlySpan<byte>(properties.RuntimeName, XrBlock.RuntimeNameLength - 1).CopyTo(target);
        target[^1] = 0;
    }

    private static XrGraphicsBindingD3D11* FindD3D11Binding(XrSessionCreateInfo* createInfo)
    {
        if (createInfo is null)
        {
            return null;
        }

        var link = (XrBaseHeader*)createInfo->Next;
        for (var i = 0; link is not null && i < MostNextLinks; i++)
        {
            if (link->Type == XrType.GraphicsBindingD3D11)
            {
                var binding = (XrGraphicsBindingD3D11*)link;
                return binding->Device == 0 ? null : binding;
            }

            link = (XrBaseHeader*)link->Next;
        }

        return null;
    }

    private long ChooseSwapchainFormat()
    {
        uint count = 0;
        if (calls.EnumerateSwapchainFormats(Handle, 0, &count, null) < XrResult.Success
            || count is 0 or > MostSwapchainFormats)
        {
            return 0;
        }

        var formats = stackalloc long[(int)count];
        if (calls.EnumerateSwapchainFormats(Handle, count, &count, formats) < XrResult.Success)
        {
            return 0;
        }

        var offered = new ReadOnlySpan<long>(formats, (int)count);
        return offered.Contains(XrConstant.FormatBgraSrgb) ? XrConstant.FormatBgraSrgb
            : offered.Contains(XrConstant.FormatBgra) ? XrConstant.FormatBgra
            : 0;
    }

    private ulong CreateReferenceSpace(int type)
    {
        var createInfo = new XrReferenceSpaceCreateInfo
        {
            Type = XrType.ReferenceSpaceCreateInfo,
            ReferenceSpaceType = type,
            PoseInReferenceSpace = new XrPosef { OrientationW = 1f },
        };
        ulong space = 0;
        return calls.CreateReferenceSpace(Handle, &createInfo, &space) < XrResult.Success ? 0 : space;
    }

    /// <summary>Whether d47 has pointing on, read from the block now. Elite's attach can come before the first frame.</summary>
    private bool ReadPointing()
    {
        if (block is null)
        {
            return false;
        }

        return XrSeqLock.TryRead(&block->D47, out var d47) ? d47.Pointing == 1 : lastD47.Pointing == 1;
    }

    private void Refuse(XrRefusal refusal)
    {
        drawing = false;
        published.Refusal = refusal;
        Publish();
    }

    /// <summary>Stops drawing for the rest of the session. Never throws.</summary>
    private void TurnOff(XrRefusal refusal)
    {
        drawing = false;
        published.Refusal = refusal;
        try
        {
            ReleaseTextures();
        }
        catch (Exception)
        {
            // Drawing is already off.
        }

        TryPublish();
    }

    private void Publish()
    {
        if (block is not null)
        {
            XrSeqLock.Write(&block->Layer, published);
        }
    }

    private void TryPublish()
    {
        try
        {
            Publish();
        }
        catch (Exception)
        {
            // Nothing else can be done from inside Elite's call.
        }
    }

    private enum CopyOutcome
    {
        Copied,
        Pending,
        Failed,
    }

    private sealed class Slot
    {
        public ulong Swapchain;
        public uint Width;
        public uint Height;
        public nint[] Images = [];
        public bool Acquired;
        public uint AcquiredIndex;
        public bool HasPicture;
        public ulong CopiedGeneration;
        public ulong FailedGeneration;
        public readonly Texture[] Textures = [new(), new()];
    }

    private sealed class Texture
    {
        public readonly char[] Name = new char[XrBlock.TextureNameLength];
        public int NameLength;
        public nint Handle;
        public uint Width;
        public uint Height;
        public long LastUse;
    }
}
