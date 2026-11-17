using System.Runtime.InteropServices;
using D47.XrLayer;

namespace D47.XrLayer.Tests;

/// <summary>An Elite session over a block in memory, with stand-ins for the next layer and the Direct3D 11 device.</summary>
internal sealed unsafe class LayerRig : IDisposable
{
    public const ulong SessionHandle = 0x5E55;
    public const ulong LocalSpace = 0x10;
    public const ulong ViewSpace = 0x11;
    public const long Millisecond = 1_000_000;

    private readonly XrBaseHeader* eliteLayer;
    private XrD47Half d47;

    public LayerRig(int bindingType = XrType.GraphicsBindingD3D11, long[]? swapchainFormats = null)
    {
        if (swapchainFormats is not null)
        {
            Next.Formats = swapchainFormats;
        }

        Block = (XrBlock*)NativeMemory.AllocZeroed((nuint)sizeof(XrBlock));
        eliteLayer = (XrBaseHeader*)NativeMemory.AllocZeroed((nuint)sizeof(XrBaseHeader));
        eliteLayer->Type = XrType.CompositionLayerProjection;

        var binding = new XrGraphicsBindingD3D11 { Type = bindingType, Device = 0xD3D };
        var createInfo = new XrSessionCreateInfo { Type = XrType.SessionCreateInfo, Next = &binding };
        Session = EliteSession.Attach(Next, Block, 0x1257, &createInfo, SessionHandle);
    }

    public XrBlock* Block { get; }

    public FakeNext Next { get; } = new();

    public FakeDevice Device => Next.Device;

    public EliteSession Session { get; }

    public XrLayerHalf Published => Block->Layer;

    /// <summary>The address of the projection layer Elite submits with every frame.</summary>
    public nint EliteLayer => (nint)eliteLayer;

    /// <summary>Puts a visible slot with its texture on the device. The quad's x position is the slot index.</summary>
    public void Show(int index, XrQuadSpace space = XrQuadSpace.Cockpit, uint width = 800, uint height = 600)
    {
        var name = $"d47.slot{index}.a";
        Device.Textures[name] = Description(width, height);
        ref var slot = ref d47.Slots[index];
        slot = default;
        slot.Visible = 1;
        slot.Space = space;
        slot.Pose = new XrBlockPose { OrientationW = 1f, PositionX = index, PositionZ = -1f };
        slot.WidthMetres = 1f;
        slot.TextureWidth = width;
        slot.TextureHeight = height;
        slot.TextureGeneration = 1;
        slot.RectWidth = (int)width;
        slot.RectHeight = (int)height / 2;
        Span<char> target = slot.TextureName;
        name.AsSpan().CopyTo(target);
        Publish();
    }

    public ref XrQuadSlot Slot(int index) => ref d47.Slots[index];

    public void Publish() => XrSeqLock.Write(&Block->D47, d47);

    public void Beat()
    {
        d47.Heartbeat++;
        Publish();
    }

    /// <summary>Two frames with the heartbeat advancing between them, after which visible slots are drawn.</summary>
    public void Run(long time = 0)
    {
        Beat();
        EndFrame(time);
        Beat();
        EndFrame(time + Millisecond);
    }

    /// <summary>Ends one frame carrying Elite's projection layer and returns what the next layer received.</summary>
    public (int Result, IReadOnlyList<FakeNext.Frame> Calls) EndFrame(long time)
    {
        var layers = stackalloc nint[1];
        layers[0] = (nint)eliteLayer;
        var info = new XrFrameEndInfo
        {
            Type = XrType.FrameEndInfo,
            DisplayTime = time,
            LayerCount = 1,
            Layers = (XrBaseHeader**)layers,
        };
        Next.Frames.Clear();
        Next.Original = (nint)(&info);
        var result = Session.EndFrame(&info);
        return (result, Next.Frames.ToList());
    }

    public static TextureDescription Description(uint width, uint height) => new()
    {
        Width = width,
        Height = height,
        MipLevels = 1,
        ArraySize = 1,
        Format = XrBlock.TextureFormat,
        SampleCount = 1,
    };

    public void Dispose()
    {
        NativeMemory.Free(Block);
        NativeMemory.Free(eliteLayer);
    }
}

/// <summary>Stands in for the next layer's functions and records each call.</summary>
internal sealed unsafe class FakeNext : ILayerCalls
{
    private ulong nextSwapchain = 0x100;

    public FakeDevice Device { get; } = new();

    public List<string> Calls { get; } = [];

    public List<Frame> Frames { get; } = [];

    public List<(ulong Handle, uint Width, uint Height)> Swapchains { get; } = [];

    public long[] Formats { get; set; } = [XrConstant.FormatBgraSrgb, 29];

    /// <summary>Rejects every frame that carries a quad, as a runtime does a malformed one.</summary>
    public bool RejectQuads { get; set; }

    public nint Original { get; set; }

    public int GetInstanceProperties(ulong instance, XrInstanceProperties* properties)
    {
        Calls.Add(nameof(GetInstanceProperties));
        "VirtualDesktopXR"u8.CopyTo(new Span<byte>(properties->RuntimeName, XrBlock.RuntimeNameLength));
        return XrResult.Success;
    }

    public int EnumerateSwapchainFormats(ulong session, uint capacity, uint* count, long* formats)
    {
        Calls.Add(nameof(EnumerateSwapchainFormats));
        *count = (uint)Formats.Length;
        if (capacity >= Formats.Length)
        {
            Formats.CopyTo(new Span<long>(formats, Formats.Length));
        }

        return XrResult.Success;
    }

    public int CreateReferenceSpace(ulong session, XrReferenceSpaceCreateInfo* createInfo, ulong* space)
    {
        Calls.Add(nameof(CreateReferenceSpace));
        *space = createInfo->ReferenceSpaceType == XrConstant.ReferenceSpaceLocal ? LayerRig.LocalSpace : LayerRig.ViewSpace;
        return XrResult.Success;
    }

    public int DestroySpace(ulong space)
    {
        Calls.Add(nameof(DestroySpace));
        return XrResult.Success;
    }

    public int LocateSpace(ulong space, ulong baseSpace, long time, XrSpaceLocation* location)
    {
        Calls.Add(nameof(LocateSpace));
        location->LocationFlags = XrConstant.LocationOrientationValid | XrConstant.LocationPositionValid;
        location->Pose = new XrPosef { OrientationW = 1f, PositionY = 1.2f };
        return XrResult.Success;
    }

    public int CreateSwapchain(ulong session, XrSwapchainCreateInfo* createInfo, ulong* swapchain)
    {
        Calls.Add(nameof(CreateSwapchain));
        *swapchain = nextSwapchain++;
        Swapchains.Add((*swapchain, createInfo->Width, createInfo->Height));
        return XrResult.Success;
    }

    public int DestroySwapchain(ulong swapchain)
    {
        Calls.Add(nameof(DestroySwapchain));
        return XrResult.Success;
    }

    public int EnumerateSwapchainImages(ulong swapchain, uint capacity, uint* count, XrSwapchainImageD3D11* images)
    {
        Calls.Add(nameof(EnumerateSwapchainImages));
        *count = 3;
        for (var i = 0; i < capacity && i < 3; i++)
        {
            images[i].Texture = SwapchainImage(swapchain, i);
        }

        return XrResult.Success;
    }

    public int AcquireSwapchainImage(ulong swapchain, uint* index)
    {
        Calls.Add(nameof(AcquireSwapchainImage));
        *index = 0;
        return XrResult.Success;
    }

    public int WaitSwapchainImage(ulong swapchain, long timeoutNanoseconds)
    {
        Calls.Add(nameof(WaitSwapchainImage));
        return XrResult.Success;
    }

    public int ReleaseSwapchainImage(ulong swapchain)
    {
        Calls.Add(nameof(ReleaseSwapchainImage));
        return XrResult.Success;
    }

    public int EndFrame(ulong session, XrFrameEndInfo* frameEndInfo)
    {
        var layers = new List<nint>();
        var quads = new List<XrCompositionLayerQuad>();
        for (var i = 0; i < frameEndInfo->LayerCount; i++)
        {
            var layer = frameEndInfo->Layers[i];
            layers.Add((nint)layer);
            if (layer->Type == XrType.CompositionLayerQuad)
            {
                quads.Add(*(XrCompositionLayerQuad*)layer);
            }
        }

        Frames.Add(new Frame((nint)frameEndInfo == Original, frameEndInfo->DisplayTime, layers, quads));
        return RejectQuads && quads.Count > 0 ? XrResult.ErrorPoseInvalid : XrResult.Success;
    }

    public ITextureDevice? OpenDevice(nint d3d11Device)
    {
        Calls.Add(nameof(OpenDevice));
        return Device;
    }

    public static nint SwapchainImage(ulong swapchain, int index) => (nint)((swapchain << 8) + (ulong)index + 1);

    /// <summary>One call to the next xrEndFrame.</summary>
    public sealed record Frame(bool IsElitesOwn, long DisplayTime, IReadOnlyList<nint> Layers, IReadOnlyList<XrCompositionLayerQuad> Quads);
}

/// <summary>Stands in for the session's Direct3D 11 device. Textures are found by name.</summary>
internal sealed unsafe class FakeDevice : ITextureDevice
{
    private readonly Dictionary<nint, string> open = [];
    private nint nextTexture = 0x7000;

    public Dictionary<string, TextureDescription> Textures { get; } = [];

    public List<(string Source, nint Destination)> Copies { get; } = [];

    public List<string> Opened { get; } = [];

    public List<string> Released { get; } = [];

    public bool ThrowOnCopy { get; set; }

    public (uint Low, int High) AdapterLuid => (0xA1, 2);

    public int OpenCount => open.Count;

    public nint OpenShared(char* name)
    {
        var text = new string(name);
        if (!Textures.ContainsKey(text))
        {
            return 0;
        }

        var texture = nextTexture++;
        open[texture] = text;
        Opened.Add(text);
        return texture;
    }

    public TextureDescription Describe(nint texture) => Textures[open[texture]];

    public void Copy(nint source, nint destination)
    {
        if (ThrowOnCopy)
        {
            throw new InvalidOperationException("copy failed");
        }

        Copies.Add((open[source], destination));
    }

    public void Release(nint texture)
    {
        Released.Add(open[texture]);
        open.Remove(texture);
    }

    public void Close()
    {
    }
}
