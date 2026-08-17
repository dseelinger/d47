using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace D47.Vr;

/// <summary>
/// One surface's pixels on their way to the compositor: a pinned buffer the panel is
/// rasterised into and <c>SetOverlayRaw</c> uploads from.
/// <para>
/// <b>This replaced a D3D11 shared texture.</b> The texture path was correct as far as anyone
/// could measure it — the copy was proven pixel-for-pixel by test, the adapter matched, the
/// share flag was set, the handle was the texture and not the share handle — and the overlay
/// was still invisible with SteamVR reporting it visible. Handing OpenVR bytes and letting it
/// do its own upload removes the entire question, along with a graphics device, an adapter
/// match, a share flag, and the one-device-per-process trap that comes with them.
/// </para>
/// <para>
/// The cost is a full-surface copy inside OpenVR per submit. Submits are per change rather
/// than per frame, and the copy this replaces was measured at 0.05 ms of a 0.75 ms frame, so
/// what is being given up is the small end of a budget that was never under pressure.
/// </para>
/// </summary>
public sealed class VrPixels
{
    /// <summary>
    /// How many buffers are kept in flight.
    /// <para>
    /// <b>One is not enough, and the reason is not obvious.</b> <c>SetOverlayRaw</c> is not
    /// documented as copying synchronously, so the runtime may still be reading the last buffer it
    /// was handed when the next frame is rasterised into it. With a panel that repaints a few times
    /// a second that is a race nothing ever loses; while a panel is being carried the uploads come
    /// every tick and it starts to show — as tearing, or as the panel flickering, and only while
    /// something is happening. COVAS++ hit exactly this and fixed it the same way.
    /// </para>
    /// <para>
    /// Four, following theirs. The cost is three more buffers of a panel-sized image, which is a
    /// couple of megabytes against a hazard that is invisible until it is a bug report.
    /// </para>
    /// </summary>
    private const int InFlight = 4;

    private byte[][] _buffers = [];
    private IntPtr[] _addresses = [];
    private int _next;

    public VrPixels(int width, int height) => Allocate(width, height);

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// Bytes per row, with no padding. <c>SetOverlayRaw</c> takes a width and a bytes-per-pixel
    /// and derives the stride, so a padded buffer would be read as a shifted image rather than
    /// refused — the rasteriser has to be told this exact number.
    /// </summary>
    public int RowBytes => Width * 4;

    /// <summary>
    /// Where to rasterise, and what to hand the runtime — the current buffer of the ring.
    /// <para>
    /// Not stable between frames any more, and nothing may cache it. It changes only in
    /// <see cref="Rotate"/>, which the serving loop calls once per upload.
    /// </para>
    /// </summary>
    public IntPtr Address { get; private set; }

    /// <summary>
    /// Moves to the next buffer in the ring. Called after an upload, so the frame the runtime was
    /// just handed is left alone until three more have been drawn.
    /// </summary>
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

    /// <summary>
    /// Turns the rasteriser's BGRA into the RGBA <c>SetOverlayRaw</c> reads, in place.
    /// <para>
    /// <b>Not optional, and its absence is not an error.</b> Avalonia rasterises BGRA and
    /// OpenVR's raw path reads RGBA; feed one to the other unconverted and every call still
    /// returns <c>None</c>, the overlay still appears, and red and blue are swapped in it.
    /// A previous project of Doug's does exactly that, which is a fair part of why its headset
    /// overlays are remembered as working "semi-OK" rather than as working.
    /// </para>
    /// <para>
    /// Alpha is left premultiplied and the overlay is flagged
    /// <see cref="Valve.VR.VROverlayFlags.IsPremultiplied"/> to say so, which is cheaper and
    /// exact where dividing it back out would be neither.
    /// </para>
    /// </summary>
    public void ToRgba()
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(_buffers[_next].AsSpan());

        for (var i = 0; i < pixels.Length; i++)
        {
            // Little-endian, so bytes B,G,R,A read as 0xAARRGGBB and the swap of the red and
            // blue *bytes* is a swap of the second and fourth byte of the word. Green and
            // alpha do not move, which is what the mask keeps.
            var pixel = pixels[i];
            pixels[i] = (pixel & 0xFF00FF00u) | ((pixel >> 16) & 0x000000FFu) | ((pixel & 0x000000FFu) << 16);
        }
    }

    /// <summary>
    /// Allocates on the pinned object heap, so the address handed to the rasteriser and to
    /// OpenVR stays put without a long-lived <see cref="GCHandle"/> pinning an ordinary array
    /// in the middle of a generation.
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
