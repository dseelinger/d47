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
    public const ulong Instance = 0x1257;
    public const string TouchProfile = "/interaction_profiles/oculus/touch_controller";

    private readonly XrBaseHeader* eliteLayer;
    private XrD47Half d47;

    public LayerRig(int bindingType = XrType.GraphicsBindingD3D11, long[]? swapchainFormats = null, bool cylinders = false)
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
        Session = EliteSession.Attach(Next, Block, Bindings, Instance, &createInfo, SessionHandle, cylinders);
    }

    public XrBlock* Block { get; }

    public FakeNext Next { get; } = new();

    /// <summary>What Elite suggested through the layer's xrSuggestInteractionProfileBindings.</summary>
    public SuggestedBindings Bindings { get; } = new();

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

    /// <summary>Sets the block's pointing switch, as d47 does.</summary>
    public void Point(bool on)
    {
        d47.Pointing = on ? 1u : 0u;
        Publish();
    }

    /// <summary>Elite's session reaching the focused state, as xrPollEvent reports it.</summary>
    public void Focus() => Session.StateChanged(XrConstant.SessionStateFocused);

    /// <summary>Elite suggesting bindings for the Touch profile, accepted by the runtime.</summary>
    public void EliteSuggests(params (ulong Action, string Binding)[] bindings)
    {
        var suggested = stackalloc XrActionSuggestedBinding[Math.Max(bindings.Length, 1)];
        for (var i = 0; i < bindings.Length; i++)
        {
            suggested[i] = new XrActionSuggestedBinding { Action = bindings[i].Action, Binding = Next.Path(bindings[i].Binding) };
        }

        var info = new XrInteractionProfileSuggestedBinding
        {
            Type = XrType.InteractionProfileSuggestedBinding,
            InteractionProfile = Next.Path(TouchProfile),
            CountSuggestedBindings = (uint)bindings.Length,
            SuggestedBindings = suggested,
        };
        Bindings.Record(Instance, &info);
    }

    /// <summary>Elite's xrAttachSessionActionSets with its own sets, through the layer.</summary>
    public int EliteAttaches(params ulong[] sets)
    {
        fixed (ulong* elites = sets)
        {
            var info = new XrSessionActionSetsAttachInfo
            {
                Type = XrType.SessionActionSetsAttachInfo,
                CountActionSets = (uint)sets.Length,
                ActionSets = elites,
            };
            Next.ElitesAttach = (nint)(&info);
            return Session.AttachActionSets(&info);
        }
    }

    /// <summary>Elite's xrSyncActions with its own sets, through the layer.</summary>
    public int EliteSyncs(params ulong[] sets)
    {
        var active = stackalloc XrActiveActionSet[Math.Max(sets.Length, 1)];
        for (var i = 0; i < sets.Length; i++)
        {
            active[i] = new XrActiveActionSet { ActionSet = sets[i] };
        }

        var info = new XrActionsSyncInfo
        {
            Type = XrType.ActionsSyncInfo,
            CountActiveActionSets = (uint)sets.Length,
            ActiveActionSets = active,
        };
        Next.ElitesSync = (nint)(&info);
        return Session.SyncActions(&info);
    }

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
    public const string LocateHand = "LocateSpace(hand)";
    public const string EliteAttachCall = "Elite's " + nameof(AttachSessionActionSets);
    public const string EliteSyncCall = "Elite's " + nameof(SyncActions);
    public const ulong LayerSet = 0xA5E7;

    private static readonly HashSet<string> ActionFunctions =
    [
        nameof(StringToPath), nameof(CreateActionSet), nameof(DestroyActionSet), nameof(CreateAction),
        nameof(SuggestInteractionProfileBindings), nameof(AttachSessionActionSets), nameof(SyncActions),
        nameof(CreateActionSpace), nameof(GetActionStateFloat), LocateHand, EliteAttachCall, EliteSyncCall,
    ];

    private readonly Dictionary<string, ulong> paths = [];
    private readonly Dictionary<ulong, (ulong Action, ulong Hand)> actionSpaces = [];
    private ulong nextSwapchain = 0x100;
    private ulong nextAction = 0xAC00;
    private ulong nextActionSpace = 0xB000;
    private bool setsAttached;

    public FakeDevice Device { get; } = new();

    public List<string> Calls { get; } = [];

    public List<Frame> Frames { get; } = [];

    public List<(ulong Handle, uint Width, uint Height)> Swapchains { get; } = [];

    public long[] Formats { get; set; } = [XrConstant.FormatBgraSrgb, 29];

    /// <summary>Rejects every frame that carries a quad, as a runtime does a malformed one.</summary>
    public bool RejectQuads { get; set; }

    /// <summary>Rejects every frame that carries a cylinder.</summary>
    public bool RejectCylinders { get; set; }

    public nint Original { get; set; }

    /// <summary>Elite's own attach info, to tell its call passed on unchanged from one the layer built.</summary>
    public nint ElitesAttach { get; set; }

    /// <summary>Elite's own sync info.</summary>
    public nint ElitesSync { get; set; }

    /// <summary>The action function calls in order, Elite's passed on unchanged named as such.</summary>
    public List<string> ActionCalls => Calls.Where(ActionFunctions.Contains).ToList();

    /// <summary>Each attach that reached the runtime: whether it was Elite's own info, and its sets.</summary>
    public List<(bool IsElitesOwn, ulong[] Sets)> Attaches { get; } = [];

    /// <summary>Each sync that reached the runtime: whether it was Elite's own info, and its active sets.</summary>
    public List<(bool IsElitesOwn, ulong[] Sets)> Syncs { get; } = [];

    public List<(ulong Profile, XrActionSuggestedBinding[] Bindings)> Suggestions { get; } = [];

    /// <summary>The actions created, by name.</summary>
    public Dictionary<string, (ulong Handle, int Type, ulong[] Subactions)> Actions { get; } = [];

    /// <summary>What every sync returns: success, or XR_SESSION_NOT_FOCUSED.</summary>
    public int SyncResult { get; set; } = XrResult.Success;

    /// <summary>Float states by action name and hand path.</summary>
    public Dictionary<(string Action, string Hand), float> Values { get; } = [];

    /// <summary>The path's id, as xrStringToPath gives it.</summary>
    public ulong Path(string path)
    {
        if (!paths.TryGetValue(path, out var id))
        {
            id = 0x9000 + (ulong)paths.Count;
            paths[path] = id;
        }

        return id;
    }

    public string PathName(ulong path) => paths.Single(p => p.Value == path).Key;

    /// <summary>The pose an action space locates at: x is 1 for an aim pose and 2 for a grip, y is -1 for the left hand and 1 for the right.</summary>
    public static XrPosef HandPose(string action, string hand) => new()
    {
        OrientationW = 1f,
        PositionX = action == "aim" ? 1f : 2f,
        PositionY = hand.EndsWith("left", StringComparison.Ordinal) ? -1f : 1f,
    };

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
        if (actionSpaces.TryGetValue(space, out var actionSpace))
        {
            Calls.Add(LocateHand);
            location->LocationFlags = XrConstant.LocationOrientationValid | XrConstant.LocationPositionValid;
            location->Pose = HandPose(ActionName(actionSpace.Action), PathName(actionSpace.Hand));
            return XrResult.Success;
        }

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
        var cylinders = new List<XrCompositionLayerCylinderKHR>();
        for (var i = 0; i < frameEndInfo->LayerCount; i++)
        {
            var layer = frameEndInfo->Layers[i];
            layers.Add((nint)layer);
            if (layer->Type == XrType.CompositionLayerQuad)
            {
                quads.Add(*(XrCompositionLayerQuad*)layer);
            }
            else if (layer->Type == XrType.CompositionLayerCylinder)
            {
                cylinders.Add(*(XrCompositionLayerCylinderKHR*)layer);
            }
        }

        Frames.Add(new Frame((nint)frameEndInfo == Original, frameEndInfo->DisplayTime, layers, quads, cylinders));
        return (RejectQuads && quads.Count > 0) || (RejectCylinders && cylinders.Count > 0)
            ? XrResult.ErrorPoseInvalid
            : XrResult.Success;
    }

    public int StringToPath(ulong instance, byte* pathString, ulong* path)
    {
        Calls.Add(nameof(StringToPath));
        *path = Path(new string((sbyte*)pathString));
        return XrResult.Success;
    }

    public int CreateActionSet(ulong instance, XrActionSetCreateInfo* createInfo, ulong* actionSet)
    {
        Calls.Add(nameof(CreateActionSet));
        *actionSet = LayerSet;
        return XrResult.Success;
    }

    public int DestroyActionSet(ulong actionSet)
    {
        Calls.Add(nameof(DestroyActionSet));
        return XrResult.Success;
    }

    public int CreateAction(ulong actionSet, XrActionCreateInfo* createInfo, ulong* action)
    {
        Calls.Add(nameof(CreateAction));
        *action = nextAction++;
        var subactions = new ReadOnlySpan<ulong>(createInfo->SubactionPaths, (int)createInfo->CountSubactionPaths).ToArray();
        Actions[new string((sbyte*)createInfo->ActionName)] = (*action, createInfo->ActionType, subactions);
        return XrResult.Success;
    }

    public int SuggestInteractionProfileBindings(ulong instance, XrInteractionProfileSuggestedBinding* suggestedBindings)
    {
        Calls.Add(nameof(SuggestInteractionProfileBindings));
        var bindings = new ReadOnlySpan<XrActionSuggestedBinding>(
            suggestedBindings->SuggestedBindings, (int)suggestedBindings->CountSuggestedBindings);
        Suggestions.Add((suggestedBindings->InteractionProfile, bindings.ToArray()));
        return XrResult.Success;
    }

    public int AttachSessionActionSets(ulong session, XrSessionActionSetsAttachInfo* attachInfo)
    {
        var elites = (nint)attachInfo == ElitesAttach;
        Calls.Add(elites ? EliteAttachCall : nameof(AttachSessionActionSets));
        Attaches.Add((elites, new ReadOnlySpan<ulong>(attachInfo->ActionSets, (int)attachInfo->CountActionSets).ToArray()));
        if (setsAttached)
        {
            return XrResult.ErrorActionSetsAlreadyAttached;
        }

        setsAttached = true;
        return XrResult.Success;
    }

    public int SyncActions(ulong session, XrActionsSyncInfo* syncInfo)
    {
        var elites = (nint)syncInfo == ElitesSync;
        Calls.Add(elites ? EliteSyncCall : nameof(SyncActions));
        var sets = new ulong[syncInfo->CountActiveActionSets];
        for (var i = 0; i < sets.Length; i++)
        {
            sets[i] = syncInfo->ActiveActionSets[i].ActionSet;
        }

        Syncs.Add((elites, sets));
        return SyncResult;
    }

    public int CreateActionSpace(ulong session, XrActionSpaceCreateInfo* createInfo, ulong* space)
    {
        Calls.Add(nameof(CreateActionSpace));
        *space = nextActionSpace++;
        actionSpaces[*space] = (createInfo->Action, createInfo->SubactionPath);
        return XrResult.Success;
    }

    public int GetActionStateFloat(ulong session, XrActionStateGetInfo* getInfo, XrActionStateFloat* state)
    {
        Calls.Add(nameof(GetActionStateFloat));
        state->CurrentState = Values.GetValueOrDefault((ActionName(getInfo->Action), PathName(getInfo->SubactionPath)));
        state->IsActive = 1;
        return XrResult.Success;
    }

    public ITextureDevice? OpenDevice(nint d3d11Device)
    {
        Calls.Add(nameof(OpenDevice));
        return Device;
    }

    public static nint SwapchainImage(ulong swapchain, int index) => (nint)((swapchain << 8) + (ulong)index + 1);

    private string ActionName(ulong action) => Actions.Single(a => a.Value.Handle == action).Key;

    /// <summary>One call to the next xrEndFrame.</summary>
    public sealed record Frame(
        bool IsElitesOwn,
        long DisplayTime,
        IReadOnlyList<nint> Layers,
        IReadOnlyList<XrCompositionLayerQuad> Quads,
        IReadOnlyList<XrCompositionLayerCylinderKHR> Cylinders);
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
