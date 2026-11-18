using System.Runtime.InteropServices;
using System.Text;
using D47.XrLayer;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>The shared block in process memory, with the layer's half written the way the layer writes it.</summary>
internal sealed unsafe class InMemoryXrBlock : IXrBlockMemory, IDisposable
{
    private readonly XrBlock* _block = (XrBlock*)NativeMemory.AllocZeroed((nuint)sizeof(XrBlock));
    private XrLayerHalf _layer;

    /// <summary>Whether a layer has created the block. While false, <see cref="Open"/> finds nothing.</summary>
    public bool Exists { get; set; }

    /// <summary>Whether a view is open right now.</summary>
    public bool IsOpen { get; private set; }

    public int Opens { get; private set; }

    public IntPtr Open()
    {
        if (!Exists)
        {
            return 0;
        }

        if (!IsOpen)
        {
            Opens++;
        }

        IsOpen = true;
        return (IntPtr)_block;
    }

    public void Close() => IsOpen = false;

    /// <summary>What the layer does in Elite's xrCreateSession: the header, attached, the runtime and the adapter.</summary>
    public void Attach(string runtime, uint layoutVersion = XrBlock.CurrentLayoutVersion, uint luidLow = 0x1234, int luidHigh = 0)
    {
        Exists = true;
        _block->Magic = XrBlock.MagicValue;
        _block->LayoutVersion = layoutVersion;
        _layer.Attached = 1;
        _layer.AdapterLuidLow = luidLow;
        _layer.AdapterLuidHigh = luidHigh;

        Span<byte> name = _layer.RuntimeName;
        name.Clear();
        Encoding.UTF8.GetBytes(runtime).CopyTo(name);
        Write();
    }

    /// <summary>One of Elite's frames: the counter advances.</summary>
    public void Frame()
    {
        _layer.FrameCounter++;
        Write();
    }

    /// <summary>Whether the layer says it draws curved slots as cylinders.</summary>
    public void Cylinders(bool on)
    {
        _layer.Cylinders = on ? 1u : 0u;
        Write();
    }

    public void Refuse(XrRefusal refusal)
    {
        _layer.Refusal = refusal;
        Write();
    }

    public void Detach()
    {
        _layer.Attached = 0;
        Write();
    }

    /// <summary>A located head at <paramref name="y"/> metres, looking down -Z.</summary>
    public void HeadAt(float y)
    {
        _layer.HeadValid = 0xF;
        _layer.Head = new XrBlockPose { OrientationW = 1f, PositionY = y };
        Write();
    }

    public void Hand(
        int index,
        float trigger,
        float grip,
        bool valid = true,
        XrButtons buttons = XrButtons.None,
        float stickX = 0f,
        float stickY = 0f)
    {
        _layer.Hands[index] = new XrHand
        {
            Aim = new XrBlockPose { OrientationW = 1f },
            Grip = new XrBlockPose { OrientationW = 1f },
            Valid = valid ? 1u : 0u,
            TriggerValue = trigger,
            GripValue = grip,
            Buttons = buttons,
            StickX = stickX,
            StickY = stickY,
        };
        Write();
    }

    /// <summary>d47's half as the layer would read it.</summary>
    public XrD47Half D47Half
    {
        get
        {
            Assert.True(XrSeqLock.TryRead(&_block->D47, out var half));
            return half;
        }
    }

    /// <summary>The name in a slot, without its terminator.</summary>
    public static string NameIn(XrQuadSlot slot)
    {
        ReadOnlySpan<char> characters = slot.TextureName;
        var end = characters.IndexOf('\0');
        return new string(end < 0 ? characters : characters[..end]);
    }

    public void Dispose() => NativeMemory.Free(_block);

    private void Write()
    {
        XrSeqLock.Write(&_block->Layer, _layer);
    }
}
