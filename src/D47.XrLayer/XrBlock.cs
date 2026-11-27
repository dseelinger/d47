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

    public const uint CurrentLayoutVersion = 5;

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

    /// <summary>1 while d47 reads the motion controllers. While it is 0 the layer makes no action call.</summary>
    public uint Pointing;
    public ulong Heartbeat;
    public XrQuadSlots Slots;

    /// <summary>Raised to ask for a copy of Elite's left eye. 0 asks for none.</summary>
    public ulong CaptureRequest;

    /// <summary>The shared texture the copy goes into, null-terminated, at the eye's format and size.</summary>
    public XrTextureName CaptureTexture;
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

    /// <summary>0 is flat, 1 wraps the full circle. The layer draws slots 0, 1 and 5 curved while <see cref="XrLayerHalf.Cylinders"/> is 1.</summary>
    public float Curvature;
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

    /// <summary>Why the layer reads no controllers in the current session. Quads are drawn regardless.</summary>
    public XrControllerRefusal ControllerRefusal;

    public uint AdapterLuidLow;
    public int AdapterLuidHigh;

    /// <summary>XrSpaceLocationFlags of <see cref="Head"/>.</summary>
    public uint HeadValid;

    /// <summary>The VIEW space in the LOCAL space at the frame's display time.</summary>
    public XrBlockPose Head;

    public XrHands Hands;

    /// <summary>xrGetInstanceProperties' runtimeName, UTF-8, null-terminated.</summary>
    public XrRuntimeName RuntimeName;

    /// <summary>1 while the layer draws a curved slot as a cylinder; 0 draws every slot flat.</summary>
    public uint Cylinders;

    /// <summary>The DXGI format of Elite's left-eye image. 0 while the layer has seen none.</summary>
    public uint EyeFormat;

    /// <summary>The size of the part of the image Elite shows to the left eye, in pixels.</summary>
    public uint EyeWidth;
    public uint EyeHeight;

    /// <summary>Why the last answered capture holds no picture.</summary>
    public XrCaptureRefusal CaptureRefusal;

    /// <summary>The <see cref="XrD47Half.CaptureRequest"/> last answered.</summary>
    public ulong CaptureServed;

    /// <summary>The <see cref="FrameCounter"/> of the frame the capture was answered at.</summary>
    public ulong CaptureFrame;

    /// <summary>The left eye in the LOCAL space, for the captured frame.</summary>
    public XrBlockPose CaptureEye;

    /// <summary>The VIEW space in the LOCAL space, for the captured frame.</summary>
    public XrBlockPose CaptureHead;

    /// <summary>XrSpaceLocationFlags of <see cref="CaptureHead"/>.</summary>
    public uint CaptureHeadValid;

    /// <summary>The left eye's field of view, for the captured frame.</summary>
    public XrBlockFov CaptureFov;
}

[InlineArray(XrBlock.HandCount)]
internal struct XrHands
{
    private XrHand hand;
}

/// <summary>One controller, in the LOCAL space at the frame's display time. Index 0 is the left hand.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrHand
{
    public XrBlockPose Aim;
    public XrBlockPose Grip;

    /// <summary>1 when a focused sync read the hand and both poses located with orientation and position. 0 leaves every field zero.</summary>
    public uint Valid;
    public float TriggerValue;
    public float GripValue;
    public XrButtons Buttons;

    /// <summary>The thumbstick, each axis from -1 to 1. 0 when the stick action is inactive.</summary>
    public float StickX;
    public float StickY;
}

/// <summary>
/// The bits of <see cref="XrHand.Buttons"/>, each at the position of its button in D47.Core's VrButton. Bits 0 and 1
/// are the trigger and grip, which the layer publishes as values and leaves clear here.
/// </summary>
[Flags]
internal enum XrButtons : uint
{
    None = 0,
    Primary = 1u << 2,
    Secondary = 1u << 3,
    Menu = 1u << 4,
    StickClick = 1u << 5,
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

/// <summary>Why the layer reads no controllers in the current session.</summary>
internal enum XrControllerRefusal
{
    None = 0,

    /// <summary>Elite attached its action sets after the layer attached its own. The runtime refused Elite's call.</summary>
    EliteAttachedAfterLayer = 1,

    /// <summary>Pointing was off when Elite attached, so the layer's set is not attached. Elite must restart.</summary>
    PointingTurnedOnAfterEliteAttached = 2,

    /// <summary>Creating, suggesting, attaching or syncing the layer's action set failed.</summary>
    SetupFailed = 3,

    Faulted = 4,
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

/// <summary>An XrFovf: the angles of the view's edges from straight ahead, in radians. Left and down are negative.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XrBlockFov
{
    public float AngleLeft;
    public float AngleRight;
    public float AngleUp;
    public float AngleDown;
}

/// <summary>Why a capture of Elite's eye holds no picture.</summary>
internal enum XrCaptureRefusal
{
    None = 0,

    /// <summary>Elite submitted no projection layer the layer could read, or its swapchain's images could not be listed.</summary>
    NoEyeImage = 1,

    /// <summary>The texture d47 named could not be opened for writing.</summary>
    TextureNotOpened = 2,

    /// <summary>The texture d47 named is not the eye image's format and size.</summary>
    TextureMismatch = 3,

    /// <summary>Elite's eye image is multisampled.</summary>
    Multisampled = 4,

    /// <summary>Elite released no eye image within <see cref="EyeCaptureLimits.MostFramesWaited"/> frames.</summary>
    NotReleased = 5,

    /// <summary>The space of Elite's projection layer could not be located.</summary>
    NoPose = 6,

    Faulted = 7,
}

/// <summary>The limits of a capture, shared by the layer and d47.</summary>
internal static class EyeCaptureLimits
{
    /// <summary>How many frames a request waits for Elite to release its eye image before it is refused.</summary>
    public const int MostFramesWaited = 90;
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
