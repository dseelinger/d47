using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace D47.XrLayer;

// The memory block d47 and the OpenXR layer share. Compiled into both the layer and D47.Vr, so it
// references nothing outside this file. Changing a size or an offset needs a new LayoutVersion.

/// <summary>The block named <see cref="Name"/>. The layer creates it and writes the header; d47 opens it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrBlock
{
    public const string Name = @"Local\D47.XrLayer";

    /// <summary>"D47X" read as a little-endian uint.</summary>
    public const uint MagicValue = 0x58373444;

    public const uint CurrentLayoutVersion = 1;

    public const int SlotCount = 6;

    public const int HandCount = 2;

    public const int TextureNameLength = 64;

    public const int RuntimeNameLength = 128;

    /// <summary>The largest texture side the layer accepts, in pixels.</summary>
    public const uint MaxTextureSide = 4096;

    /// <summary>The widest quad the layer accepts, in metres.</summary>
    public const float MaxWidthMetres = 10f;

    /// <summary>The furthest a quad's centre may be from its space's origin on any axis, in metres.</summary>
    public const float MaxDistanceMetres = 100f;

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM, the format of every shared texture.</summary>
    public const uint TextureFormat = 87;

    public uint Magic;
    public uint LayoutVersion;
    public XrD47Half D47;
    public XrLayerHalf Layer;
}

/// <summary>Written by d47 only. <see cref="Sequence"/> is the first field and is odd during a write.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrD47Half
{
    public uint Sequence;
    public uint Pointing;
    public ulong Heartbeat;
    public XrQuadSlots Slots;
}

[InlineArray(XrBlock.SlotCount)]
internal struct XrQuadSlots
{
    private XrQuadSlot slot;
}

/// <summary>One quad d47 asks the layer to draw.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrQuadSlot
{
    /// <summary>1 to draw, 0 not to.</summary>
    public uint Visible;
    public XrQuadSpace Space;
    public XrBlockPose Pose;
    public float WidthMetres;
    public uint TextureWidth;
    public uint TextureHeight;

    /// <summary>Changes each time the named texture holds a new picture. 0 means none yet.</summary>
    public ulong TextureGeneration;

    /// <summary>The part of the texture to show, in pixels.</summary>
    public int RectX;
    public int RectY;
    public int RectWidth;
    public int RectHeight;

    /// <summary>The shared texture's name, null-terminated.</summary>
    public XrTextureName TextureName;
}

[InlineArray(XrBlock.TextureNameLength)]
internal struct XrTextureName
{
    private char character;
}

internal enum XrQuadSpace : uint
{
    /// <summary>The VIEW reference space: the quad moves with the head.</summary>
    Head = 0,

    /// <summary>The LOCAL reference space: the quad stays in the cockpit.</summary>
    Cockpit = 1,
}

/// <summary>Written by the layer only. <see cref="Sequence"/> is the first field and is odd during a write.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrLayerHalf
{
    public uint Sequence;

    /// <summary>1 while the layer is inside an Elite session.</summary>
    public uint Attached;

    /// <summary>Advances on every xrEndFrame of the session.</summary>
    public ulong FrameCounter;

    public XrRefusal Refusal;
    public uint AdapterLuidLow;
    public int AdapterLuidHigh;

    /// <summary>XrSpaceLocationFlags of <see cref="Head"/>.</summary>
    public uint HeadValid;

    /// <summary>The VIEW space in the LOCAL space at the frame's display time.</summary>
    public XrBlockPose Head;

    public XrHands Hands;

    /// <summary>xrGetInstanceProperties' runtimeName, UTF-8, null-terminated.</summary>
    public XrRuntimeName RuntimeName;
}

[InlineArray(XrBlock.HandCount)]
internal struct XrHands
{
    private XrHand hand;
}

/// <summary>One controller. Index 0 is the left hand.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrHand
{
    public XrBlockPose Aim;
    public XrBlockPose Grip;
    public uint Valid;
    public float TriggerValue;
    public float GripValue;
}

[InlineArray(XrBlock.RuntimeNameLength)]
internal struct XrRuntimeName
{
    private byte character;
}

/// <summary>Why the layer draws nothing in the current session.</summary>
internal enum XrRefusal
{
    None = 0,
    NotDirect3D11 = 1,
    NoSwapchainFormat = 2,
    SetupFailed = 3,
    FrameRejected = 4,
    Faulted = 5,
}

/// <summary>An XrPosef: orientation as a quaternion, then position in metres.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrBlockPose
{
    public float OrientationX;
    public float OrientationY;
    public float OrientationZ;
    public float OrientationW;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
}

/// <summary>Reads and writes a half whose first field is its sequence counter. A reader never waits.</summary>
internal static unsafe class XrSeqLock
{
    /// <summary>Copies the half, or returns false when a write is in progress or happened during the copy.</summary>
    public static bool TryRead<T>(T* half, out T copy)
        where T : unmanaged
    {
        var sequence = (uint*)half;
        var before = Volatile.Read(ref *sequence);
        if ((before & 1) != 0)
        {
            copy = default;
            return false;
        }

        Interlocked.MemoryBarrier();
        copy = *half;
        Interlocked.MemoryBarrier();
        return Volatile.Read(ref *sequence) == before;
    }

    /// <summary>Writes <paramref name="value"/> over the half. Only the half's one writer calls this.</summary>
    public static void Write<T>(T* half, T value)
        where T : unmanaged
    {
        var sequence = (uint*)half;
        var start = Volatile.Read(ref *sequence) | 1;
        Volatile.Write(ref *sequence, start);
        Interlocked.MemoryBarrier();
        *(uint*)&value = start;
        *half = value;
        Interlocked.MemoryBarrier();
        Volatile.Write(ref *sequence, start + 1);
    }
}
