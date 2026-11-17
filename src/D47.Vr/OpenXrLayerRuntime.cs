using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using D47.Core.Vr;
using D47.XrLayer;
using Microsoft.Extensions.Logging;

namespace D47.Vr;

/// <summary>
/// The headset through d47's OpenXR layer in Elite: each surface is a quad slot in the shared block, drawn from
/// a texture shared by name on the adapter the layer named. Every write to d47's half of the block is made
/// holding one lock, because <see cref="Reposition"/>, <see cref="AimBeam"/> and <see cref="ShowCursor"/> are
/// called from the aim thread.
/// </summary>
public sealed unsafe class OpenXrLayerRuntime(
    IReadOnlyList<IVrSurfaceSource> sources,
    ILogger<OpenXrLayerRuntime> logger,
    IXrBlockMemory memory,
    IVrTextureDevice textures) : IVrSession
{
    /// <summary>How long the layer's frame counter may stand still before the session ends.</summary>
    public static readonly TimeSpan StallLimit = TimeSpan.FromSeconds(1);

    /// <summary>A trigger or grip value at or above this is pressed.</summary>
    public const float PressedAt = 0.55f;

    /// <summary>A pressed trigger or grip stays pressed until its value falls to this.</summary>
    public const float ReleasedAt = 0.45f;

    public const string NoLayer = "No OpenXR runtime has the D47 layer in an Elite session.";

    public const string OlderLayer = "Elite loaded an older D47 layer. Restart Elite.";

    public const string NewerLayer = "Elite loaded a newer D47 layer than this copy of D47. Restart D47.";

    public const string NoFrames = "Elite has the D47 layer loaded but is not drawing frames.";

    internal const int PanelSlot = 0;
    internal const int AvatarSlot = 1;
    internal const int CaptionSlot = 2;
    internal const int BeamSlot = 3;
    internal const int CursorSlot = 4;

    private static readonly VrCursor[] CursorKinds = Enum.GetValues<VrCursor>();

    /// <summary>Held for every read of <see cref="_block"/> and every write to d47's half.</summary>
    private readonly Lock _half = new();

    private XrBlock* _block;
    private XrD47Half _d47;
    private XrLayerHalf _layer;

    /// <summary>The layer's frame counter at the last look, and whether it had moved since the one before.</summary>
    private ulong? _lastCounter;

    private ulong _counterAtServe;
    private DateTimeOffset? _counterMovedAt;
    private XrControllerRefusal _controllerRefusal;

    private readonly Dictionary<int, TexturePair> _pairs = [];
    private readonly Dictionary<int, VrPixels> _buffers = [];
    private readonly Dictionary<int, FrameHeld> _frames = [];
    private readonly HashSet<int> _refused = [];
    private readonly HashSet<int> _served = [];

    private Sprite? _beam;
    private readonly Dictionary<VrCursor, Sprite> _cursors = [];
    private bool _spritesRefused;

    private bool _deviceOpen;
    private ulong _generation;
    private int _serial;

    private bool _pointing;
    private bool _wanted;
    private bool _triggerDown;
    private bool _backWasDown;

    /// <summary>What one look at the block found.</summary>
    internal readonly record struct Sighting(
        bool Mapped,
        uint Magic,
        uint LayoutVersion,
        bool Attached,
        bool Moving,
        XrRefusal Refusal,
        string RuntimeName)
    {
        /// <summary>The layer is in an Elite session with this build's layout.</summary>
        public bool Present => Mapped && Magic == XrBlock.MagicValue && LayoutVersion == XrBlock.CurrentLayoutVersion && Attached;

        /// <summary>SteamVR's OpenXR runtime reports itself as <c>SteamVR/OpenXR</c>.</summary>
        public bool OnSteamVr => RuntimeName.StartsWith("SteamVR", StringComparison.OrdinalIgnoreCase);
    }

    public bool Pointing
    {
        get => _pointing;
        set
        {
            lock (_half)
            {
                _pointing = value;
                _d47.Pointing = value ? 1u : 0u;
                Publish();
            }
        }
    }

    public bool SupportsCurvature => false;

    public VrPose? Head { get; private set; }

    /// <summary>Looks at the block, and notes the frame counter so the next look can say whether it moved.</summary>
    internal Sighting Look()
    {
        lock (_half)
        {
            var block = _block;
            var temporary = false;

            if (block is null)
            {
                block = (XrBlock*)memory.Open();
                temporary = true;
            }

            try
            {
                if (block is null)
                {
                    _lastCounter = null;
                    return new Sighting(false, 0, 0, false, false, XrRefusal.None, string.Empty);
                }

                var magic = Volatile.Read(ref block->Magic);
                var version = Volatile.Read(ref block->LayoutVersion);

                if (magic != XrBlock.MagicValue || version != XrBlock.CurrentLayoutVersion)
                {
                    _lastCounter = null;
                    return new Sighting(true, magic, version, false, false, XrRefusal.None, string.Empty);
                }

                if (XrSeqLock.TryRead(&block->Layer, out var layer))
                {
                    _layer = layer;
                }

                var moving = _lastCounter is { } last && last != _layer.FrameCounter;
                _lastCounter = _layer.FrameCounter;

                return new Sighting(
                    true, magic, version, _layer.Attached == 1, moving, _layer.Refusal, RuntimeName(_layer));
            }
            finally
            {
                if (temporary)
                {
                    memory.Close();
                }
            }
        }
    }

    public VrStart Start() => Start(Look());

    internal VrStart Start(Sighting seen)
    {
        if (!seen.Mapped || seen.Magic != XrBlock.MagicValue)
        {
            return new VrStart(VrStartOutcome.NotReady, NoLayer);
        }

        if (seen.LayoutVersion != XrBlock.CurrentLayoutVersion)
        {
            return new VrStart(
                VrStartOutcome.NotReady,
                seen.LayoutVersion < XrBlock.CurrentLayoutVersion ? OlderLayer : NewerLayer);
        }

        if (!seen.Attached)
        {
            return new VrStart(VrStartOutcome.NotReady, NoLayer);
        }

        if (seen.Refusal != XrRefusal.None)
        {
            return new VrStart(VrStartOutcome.NotReady, Describe(seen.Refusal));
        }

        if (!seen.Moving)
        {
            return new VrStart(VrStartOutcome.NotReady, NoFrames);
        }

        lock (_half)
        {
            _block = (XrBlock*)memory.Open();

            if (_block is null)
            {
                return new VrStart(VrStartOutcome.NotReady, NoLayer);
            }

            var opened = textures.OpenOnLuid(_layer.AdapterLuidLow, _layer.AdapterLuidHigh);

            if (opened is not null)
            {
                _block = null;
                memory.Close();
                return new VrStart(VrStartOutcome.Failed, $"D47 could not reach Elite's graphics adapter: {opened}");
            }

            _deviceOpen = true;
            _d47 = default;
            _d47.Pointing = _pointing ? 1u : 0u;
            Publish();
        }

        _counterAtServe = _layer.FrameCounter;
        _counterMovedAt = null;
        _controllerRefusal = XrControllerRefusal.None;

        logger.LogInformation(
            "Headset surfaces go through the D47 OpenXR layer on {Runtime}",
            seen.RuntimeName.Length > 0 ? seen.RuntimeName : "an unnamed runtime");

        return VrStart.Started;
    }

    public bool Serve(DateTimeOffset now)
    {
        XrLayerHalf layer;

        lock (_half)
        {
            if (_block is null)
            {
                return false;
            }

            if (XrSeqLock.TryRead(&_block->Layer, out var read))
            {
                _layer = read;
            }

            layer = _layer;
        }

        if (layer.FrameCounter != _counterAtServe || _counterMovedAt is null)
        {
            _counterAtServe = layer.FrameCounter;
            _counterMovedAt = now;
        }
        else if (now - _counterMovedAt >= StallLimit)
        {
            logger.LogInformation("Elite has drawn no frame through the D47 layer for a second");
            return false;
        }

        if (layer.Attached != 1)
        {
            logger.LogInformation("Elite's OpenXR session has ended");
            return false;
        }

        if (layer.Refusal != XrRefusal.None)
        {
            logger.LogWarning("The D47 layer stopped drawing: {Reason}", Describe(layer.Refusal));
            return false;
        }

        NoteControllers(layer.ControllerRefusal);

        if (HeadOf(layer) is { } head)
        {
            Head = head;
        }

        if (Pointing)
        {
            MakeSprites();
        }

        foreach (var source in sources)
        {
            if (Head is { } seen)
            {
                source.Observe(seen);
            }

            Serve(source);
        }

        lock (_half)
        {
            if (!_pointing)
            {
                _d47.Slots[BeamSlot].Visible = 0;
                _d47.Slots[CursorSlot].Visible = 0;
            }

            _d47.Heartbeat++;
            Publish();
        }

        return true;
    }

    public void Stop()
    {
        lock (_half)
        {
            if (_block is not null)
            {
                for (var i = 0; i < XrBlock.SlotCount; i++)
                {
                    _d47.Slots[i].Visible = 0;
                }

                Publish();
                _block = null;
                memory.Close();
            }

            foreach (var pair in _pairs.Values)
            {
                pair.Release(textures);
            }

            _pairs.Clear();

            if (_beam is { } beam)
            {
                textures.Release(beam.Texture);
            }

            foreach (var cursor in _cursors.Values)
            {
                textures.Release(cursor.Texture);
            }

            _beam = null;
            _cursors.Clear();
            _spritesRefused = false;

            if (_deviceOpen)
            {
                textures.Close();
                _deviceOpen = false;
            }

            _d47 = default;
            _buffers.Clear();
            _frames.Clear();
            _refused.Clear();
            _served.Clear();
            _wanted = false;
            _triggerDown = false;
            _backWasDown = false;
            Head = null;
        }
    }

    public (IReadOnlyList<VrHand> Hands, VrPose? Head) HandsAndHead()
    {
        lock (_half)
        {
            if (_block is null)
            {
                return ([], null);
            }

            if (XrSeqLock.TryRead(&_block->Layer, out var read))
            {
                _layer = read;
            }

            var head = HeadOf(_layer);

            if (!_pointing)
            {
                return ([], head);
            }

            var found = new List<VrHand>(XrBlock.HandCount);

            for (var i = 0; i < XrBlock.HandCount; i++)
            {
                var hand = _layer.Hands[i];

                if (hand.Valid == 1)
                {
                    found.Add(new VrHand((uint)i, ToPose(hand.Grip), ToPose(hand.Aim)));
                }
            }

            return (found, head);
        }
    }

    public void PrepareInput()
    {
    }

    public bool TriggerHeld(bool wanted)
    {
        _wanted = wanted;

        if (!wanted)
        {
            _triggerDown = false;
            _backWasDown = false;
            return false;
        }

        _triggerDown = Pressed(_triggerDown, hand => hand.TriggerValue);
        return _triggerDown;
    }

    public bool BackPressed()
    {
        if (!_wanted)
        {
            _backWasDown = false;
            return false;
        }

        var down = Pressed(_backWasDown, hand => hand.GripValue);
        var pressed = down && !_backWasDown;
        _backWasDown = down;
        return pressed;
    }

    public void ReleaseInput()
    {
        _wanted = false;
        _triggerDown = false;
        _backWasDown = false;
    }

    public void Reposition(VrSurface surface, VrPose where)
    {
        lock (_half)
        {
            if (_block is null)
            {
                return;
            }

            ref var slot = ref _d47.Slots[SlotFor(surface)];
            slot.Space = XrQuadSpace.Cockpit;
            slot.Pose = ToBlock(where);
            Publish();
        }
    }

    public void AimBeam(VrPose? along, VrPose head, float lengthMetres)
    {
        lock (_half)
        {
            if (_block is null || _beam is not { } beam)
            {
                return;
            }

            ref var slot = ref _d47.Slots[BeamSlot];

            if (along is not { } aim || !_pointing)
            {
                slot.Visible = 0;
            }
            else
            {
                Fill(ref slot, beam);
                slot.Space = XrQuadSpace.Cockpit;
                slot.Pose = ToBlock(VrAim.BeamAlong(aim, head.Position, lengthMetres));
                slot.WidthMetres = VrAim.BeamWidthFor(lengthMetres);
                slot.Visible = 1;
            }

            Publish();
        }
    }

    public void ShowCursor(Vector3? at, VrPose head, VrCursor kind)
    {
        lock (_half)
        {
            if (_block is null || !_cursors.TryGetValue(kind, out var sprite))
            {
                return;
            }

            ref var slot = ref _d47.Slots[CursorSlot];

            if (at is not { } point || !_pointing)
            {
                slot.Visible = 0;
            }
            else
            {
                Fill(ref slot, sprite);
                slot.Space = XrQuadSpace.Cockpit;
                slot.Pose = ToBlock(VrAim.CursorAt(point, head.Position));
                slot.WidthMetres = VrAim.CursorSizeMetres;
                slot.Visible = 1;
            }

            Publish();
        }
    }

    /// <summary>The quad slot a surface is drawn in. The full and mini panel share one.</summary>
    internal static int SlotFor(VrSurface surface) => surface switch
    {
        VrSurface.Avatar => AvatarSlot,
        VrSurface.Captions => CaptionSlot,
        _ => PanelSlot,
    };

    /// <summary>A refusal, as the reason a start gives.</summary>
    internal static string Describe(XrRefusal refusal) => refusal switch
    {
        XrRefusal.NotDirect3D11 => "The D47 layer cannot draw in Elite: its OpenXR session is not Direct3D 11.",
        XrRefusal.NoSwapchainFormat => "The D47 layer cannot draw in Elite: the OpenXR runtime offers no BGRA swapchain format.",
        XrRefusal.SetupFailed => "The D47 layer could not set up in Elite's OpenXR session. Restart Elite.",
        XrRefusal.FrameRejected => "The OpenXR runtime rejected a frame with D47's quads. Restart Elite.",
        XrRefusal.Faulted => "The D47 layer faulted in Elite's OpenXR session. Restart Elite.",
        _ => $"The D47 layer refused Elite's session ({refusal}).",
    };

    private void Serve(IVrSurfaceSource source)
    {
        var index = SlotFor(source.Surface);

        if (!source.Visible)
        {
            Hide(index);
            return;
        }

        var placement = source.Placement.Sane();
        var (width, height) = source.Size;

        if (Pair(index, source, width, height) is not { } pair)
        {
            Hide(index);
            return;
        }

        var held = _frames.TryGetValue(index, out var carried) ? carried : default;
        var redraw = source.IsDirty || pair.DrawnSize != (width, height) || !pair.Opacity.Equals(placement.Opacity);
        var plan = FrameDelivery.Plan(held, redraw, isVisible: true);

        if (plan.Draw)
        {
            if (!_buffers.TryGetValue(index, out var pixels))
            {
                pixels = new VrPixels(width, height);
                _buffers[index] = pixels;
            }

            pixels.Resize(width, height);
            source.Draw(pixels.Address, pixels.RowBytes);
            Fade(pixels, placement.Opacity);

            textures.Write(pair.Spare, pixels.Address, pixels.RowBytes, width, height);
            textures.Flush();
            pair.Drawn((width, height), placement.Opacity);
        }

        if (plan.Submit)
        {
            var outcome = FrameDelivery.Took(held, accepted: true);

            if (outcome.Rotate)
            {
                pair.Rotate(++_generation);
            }

            held = outcome.Held;
        }

        _frames[index] = held;

        if (pair.Generation == 0)
        {
            Hide(index);
            return;
        }

        var head = Head ?? VrPose.Origin;
        var rides = placement.RidesTheHead;
        var pose = rides ? placement.AgainstTheHead(head) : placement.Where(head);

        lock (_half)
        {
            ref var slot = ref _d47.Slots[index];
            slot.Space = rides ? XrQuadSpace.Head : XrQuadSpace.Cockpit;
            slot.Pose = ToBlock(pose);
            slot.WidthMetres = placement.WidthMetres;
            slot.TextureWidth = (uint)pair.Width;
            slot.TextureHeight = (uint)pair.Height;
            slot.TextureGeneration = pair.Generation;
            slot.RectX = 0;
            slot.RectY = 0;
            slot.RectWidth = pair.PublishedSize.Width;
            slot.RectHeight = pair.PublishedSize.Height;
            Name(ref slot, pair.CurrentName);
            slot.Visible = 1;
        }

        if (_served.Add(index))
        {
            logger.LogInformation(
                "{Surface} is up through the D47 layer: {Width}x{Height} in a {TextureWidth}x{TextureHeight} texture, "
                + "{Metres:0.00}m wide, {Space}",
                source.Surface,
                width,
                height,
                pair.Width,
                pair.Height,
                placement.WidthMetres,
                rides ? "on the head" : "in the cockpit");
        }
    }

    /// <summary>
    /// The two textures a slot alternates between, made at the largest size its surface can take and remade
    /// only when its size goes past that. Null, said once, when they cannot be made.
    /// </summary>
    private TexturePair? Pair(int index, IVrSurfaceSource source, int width, int height)
    {
        if (_pairs.TryGetValue(index, out var pair) && pair.Width >= width && pair.Height >= height)
        {
            return pair;
        }

        if (_refused.Contains(index))
        {
            return null;
        }

        if (pair is not null)
        {
            Hide(index);
            pair.Release(textures);
            _pairs.Remove(index);
        }

        _frames.Remove(index);

        var (largestWidth, largestHeight) = source.LargestSize;
        var allocateWidth = Math.Clamp(Math.Max(largestWidth, width), 1, (int)XrBlock.MaxTextureSide);
        var allocateHeight = Math.Clamp(Math.Max(largestHeight, height), 1, (int)XrBlock.MaxTextureSide);

        if (width > allocateWidth || height > allocateHeight)
        {
            return Refuse(index, source, $"{width}x{height} is larger than the layer takes");
        }

        var first = NextName();
        var second = NextName();
        var a = textures.CreateShared(allocateWidth, allocateHeight, first, out var refused);

        if (refused is not null)
        {
            return Refuse(index, source, refused);
        }

        var b = textures.CreateShared(allocateWidth, allocateHeight, second, out refused);

        if (refused is not null)
        {
            textures.Release(a);
            return Refuse(index, source, refused);
        }

        pair = new TexturePair([a, b], [first, second], allocateWidth, allocateHeight);
        _pairs[index] = pair;
        return pair;
    }

    private TexturePair? Refuse(int index, IVrSurfaceSource source, string why)
    {
        _refused.Add(index);
        logger.LogWarning("{Surface} is not shown through the D47 layer this session: {Reason}", source.Surface, why);
        return null;
    }

    /// <summary>The beam and the five cursors, each a texture written once.</summary>
    private void MakeSprites()
    {
        if (_beam is not null || _spritesRefused)
        {
            return;
        }

        var beam = MakeSprite(VrSprites.Beam(), VrAim.BeamPixelsWide, VrAim.BeamPixelsTall);
        var cursors = new Dictionary<VrCursor, Sprite>();

        foreach (var kind in CursorKinds)
        {
            if (beam is null)
            {
                break;
            }

            if (MakeSprite(VrSprites.Cursor(kind), VrSprites.CursorSize, VrSprites.CursorSize) is not { } cursor)
            {
                beam = null;
                break;
            }

            cursors[kind] = cursor;
        }

        if (beam is null)
        {
            foreach (var made in cursors.Values)
            {
                textures.Release(made.Texture);
            }

            _spritesRefused = true;
            logger.LogWarning("The aim beam and cursor could not be made for the D47 layer; pointing goes unguided");
            return;
        }

        textures.Flush();

        lock (_half)
        {
            _beam = beam;

            foreach (var (kind, cursor) in cursors)
            {
                _cursors[kind] = cursor;
            }
        }
    }

    private Sprite? MakeSprite(byte[] rgba, int width, int height)
    {
        var name = NextName();
        var texture = textures.CreateShared(width, height, name, out var refused);

        if (refused is not null)
        {
            return null;
        }

        var bgra = Bgra(rgba);
        var pinned = GCHandle.Alloc(bgra, GCHandleType.Pinned);

        try
        {
            textures.Write(texture, pinned.AddrOfPinnedObject(), width * 4, width, height);
        }
        finally
        {
            pinned.Free();
        }

        return new Sprite(texture, name, width, height, ++_generation);
    }

    /// <summary>RGBA as <see cref="VrSprites"/> draws it, swapped to the BGRA every shared texture holds.</summary>
    private static byte[] Bgra(byte[] rgba)
    {
        var bgra = (byte[])rgba.Clone();

        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        }

        return bgra;
    }

    /// <summary>Multiplies every channel of premultiplied BGRA by <paramref name="opacity"/>, since a quad layer has none.</summary>
    private static void Fade(VrPixels pixels, float opacity)
    {
        if (opacity >= 1f)
        {
            return;
        }

        var scale = (uint)Math.Round(opacity * 256f);
        var bytes = new Span<byte>((void*)pixels.Address, pixels.RowBytes * pixels.Height);

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((bytes[i] * scale) >> 8);
        }
    }

    private string NextName() => $"D47.{Environment.ProcessId}.{++_serial}";

    private void Hide(int index)
    {
        lock (_half)
        {
            _d47.Slots[index].Visible = 0;
        }
    }

    private static void Fill(ref XrQuadSlot slot, Sprite sprite)
    {
        slot.TextureWidth = (uint)sprite.Width;
        slot.TextureHeight = (uint)sprite.Height;
        slot.TextureGeneration = sprite.Generation;
        slot.RectX = 0;
        slot.RectY = 0;
        slot.RectWidth = sprite.Width;
        slot.RectHeight = sprite.Height;
        Name(ref slot, sprite.Name);
    }

    private static void Name(ref XrQuadSlot slot, string name)
    {
        Span<char> characters = slot.TextureName;
        characters.Clear();
        name.AsSpan(0, Math.Min(name.Length, XrBlock.TextureNameLength - 1)).CopyTo(characters);
    }

    /// <summary>Writes d47's half. Call holding <see cref="_half"/>.</summary>
    private void Publish()
    {
        if (_block is not null)
        {
            XrSeqLock.Write(&_block->D47, _d47);
        }
    }

    private bool Pressed(bool was, Func<XrHand, float> value)
    {
        XrLayerHalf layer;

        lock (_half)
        {
            if (_block is not null && XrSeqLock.TryRead(&_block->Layer, out var read))
            {
                _layer = read;
            }

            layer = _layer;
        }

        var threshold = was ? ReleasedAt : PressedAt;

        for (var i = 0; i < XrBlock.HandCount; i++)
        {
            var hand = layer.Hands[i];

            if (hand.Valid == 1 && value(hand) >= threshold)
            {
                return true;
            }
        }

        return false;
    }

    private void NoteControllers(XrControllerRefusal refusal)
    {
        if (refusal == _controllerRefusal)
        {
            return;
        }

        _controllerRefusal = refusal;

        if (refusal == XrControllerRefusal.None)
        {
            logger.LogInformation("The D47 layer is reading the motion controllers");
            return;
        }

        logger.LogWarning("The D47 layer reads no motion controllers: {Reason}", refusal switch
        {
            XrControllerRefusal.EliteAttachedAfterLayer => "Elite attached its controls after the layer did",
            XrControllerRefusal.PointingTurnedOnAfterEliteAttached =>
                "pointing was turned on after Elite attached its controls; restart Elite",
            XrControllerRefusal.SetupFailed => "the layer's controller actions could not be set up",
            XrControllerRefusal.Faulted => "the layer faulted reading them",
            _ => refusal.ToString(),
        });
    }

    /// <summary>The head, when the layer located both its orientation and its position.</summary>
    private static VrPose? HeadOf(in XrLayerHalf layer) =>
        (layer.HeadValid & 0x3) == 0x3 && ToPose(layer.Head) is { IsFinite: true } head ? head : null;

    private static string RuntimeName(in XrLayerHalf layer)
    {
        ReadOnlySpan<byte> bytes = layer.RuntimeName;
        var end = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private static VrPose ToPose(XrBlockPose pose) => new(
        new Vector3(pose.PositionX, pose.PositionY, pose.PositionZ),
        new Quaternion(pose.OrientationX, pose.OrientationY, pose.OrientationZ, pose.OrientationW));

    private static XrBlockPose ToBlock(VrPose pose)
    {
        var orientation = Quaternion.Normalize(pose.Orientation);

        return new XrBlockPose
        {
            OrientationX = orientation.X,
            OrientationY = orientation.Y,
            OrientationZ = orientation.Z,
            OrientationW = orientation.W,
            PositionX = pose.Position.X,
            PositionY = pose.Position.Y,
            PositionZ = pose.Position.Z,
        };
    }

    private sealed record Sprite(IntPtr Texture, string Name, int Width, int Height, ulong Generation);

    /// <summary>Two shared textures: the one the layer was last given, and the spare the next frame is drawn into.</summary>
    private sealed class TexturePair(IntPtr[] handles, string[] names, int width, int height)
    {
        private int _current = 1;
        private (int Width, int Height) _spareSize;

        public int Width => width;

        public int Height => height;

        public IntPtr Spare => handles[1 - _current];

        public string CurrentName => names[_current];

        /// <summary>0 until a frame has been published.</summary>
        public ulong Generation { get; private set; }

        /// <summary>The size drawn into the texture the layer was last given.</summary>
        public (int Width, int Height) PublishedSize { get; private set; }

        /// <summary>The size of the last draw, published or not.</summary>
        public (int Width, int Height) DrawnSize { get; private set; }

        public float Opacity { get; private set; } = float.NaN;

        public void Drawn((int Width, int Height) size, float opacity)
        {
            _spareSize = size;
            DrawnSize = size;
            Opacity = opacity;
        }

        /// <summary>Makes the spare the current texture, under a new generation.</summary>
        public void Rotate(ulong generation)
        {
            _current = 1 - _current;
            PublishedSize = _spareSize;
            Generation = generation;
        }

        public void Release(IVrTextureDevice device)
        {
            foreach (var handle in handles)
            {
                device.Release(handle);
            }
        }
    }
}
