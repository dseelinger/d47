using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace D47.Vr;

/// <summary>
/// One surface's pixels on their way to the compositor: a pinned buffer the panel is rasterised into
/// and <c>SetOverlayRaw</c> uploads from.
/// </summary>
public sealed class VrPixels
{
    /// <summary>How many buffers are kept in flight.</summary>
    private const int InFlight = 4;

    private byte[][] _buffers = [];
    private IntPtr[] _addresses = [];
    private int _next;

    public VrPixels(int width, int height) => Allocate(width, height);

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Bytes per row, with no padding.</summary>
    public int RowBytes => Width * 4;

    /// <summary>Where to rasterise, and what to hand the runtime — the current buffer of the ring.</summary>
    public IntPtr Address { get; private set; }

    /// <summary>Moves to the next buffer in the ring.</summary>
    public void Rotate()
    {
        _next = (_next + 1) % InFlight;
        Address = _addresses[_next];
    }

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return;
        }

        Allocate(width, height);
    }

    /// <summary>Turns the rasteriser's BGRA into the RGBA <c>SetOverlayRaw</c> reads, in place.</summary>
    public void ToRgba()
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(_buffers[_next].AsSpan());

        for (var i = 0; i < pixels.Length; i++)
        {
            // Little-endian, so bytes B,G,R,A read as 0xAARRGGBB and the swap of the red and blue *bytes* is
            // a swap of the second and fourth byte of the word.
            var pixel = pixels[i];
            pixels[i] = (pixel & 0xFF00FF00u) | ((pixel >> 16) & 0x000000FFu) | ((pixel & 0x000000FFu) << 16);
        }
    }

    /// <summary>
    /// Allocates on the pinned object heap, so the address handed to the rasteriser and to OpenVR stays
    /// put without a long-lived <see cref="GCHandle"/> pinning an ordinary array in the middle of a
    /// generation.
    /// </summary>
    private void Allocate(int width, int height)
    {
        Width = width;
        Height = height;
        _buffers = new byte[InFlight][];
        _addresses = new IntPtr[InFlight];
        _next = 0;

        for (var i = 0; i < InFlight; i++)
        {
            _buffers[i] = GC.AllocateArray<byte>(width * height * 4, pinned: true);

            unsafe
            {
                _addresses[i] = (IntPtr)Unsafe.AsPointer(
                    ref MemoryMarshal.GetArrayDataReference(_buffers[i]));
            }
        }

        Address = _addresses[0];
    }
}
