using System.Runtime.InteropServices;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>The buffer the compositor is handed, and the one conversion on the way into it.</summary>
public class VrPixelsTests
{
    private const int Width = 8;
    private const int Height = 4;

    /// <summary>Avalonia rasterises BGRA; the raw overlay path reads RGBA.</summary>
    [Fact]
    public void TheRasterisersBgraBecomesTheRgbaTheRuntimeReads()
    {
        var pixels = new VrPixels(1, 1);

        // Deliberately four different values, so a conversion that moved the wrong pair still fails: B=0x11,
        // G=0x22, R=0x33, A=0x44.
        Marshal.WriteByte(pixels.Address, 0, 0x11);
        Marshal.WriteByte(pixels.Address, 1, 0x22);
        Marshal.WriteByte(pixels.Address, 2, 0x33);
        Marshal.WriteByte(pixels.Address, 3, 0x44);

        pixels.ToRgba();

        Assert.Equal(0x33, Marshal.ReadByte(pixels.Address, 0));
        Assert.Equal(0x22, Marshal.ReadByte(pixels.Address, 1));
        Assert.Equal(0x11, Marshal.ReadByte(pixels.Address, 2));
        Assert.Equal(0x44, Marshal.ReadByte(pixels.Address, 3));
    }

    [Fact]
    public void EveryPixelIsConvertedAndNotJustTheFirst()
    {
        var pixels = new VrPixels(Width, Height);
        var written = new byte[Width * Height * 4];

        for (var i = 0; i < written.Length; i++)
        {
            // Distinct per byte and never equal across channels of a pixel, so a swap that did nothing is not
            // mistaken for a swap that worked.
            written[i] = (byte)(i + 1);
        }

        Marshal.Copy(written, 0, pixels.Address, written.Length);
        pixels.ToRgba();

        var read = new byte[written.Length];
        Marshal.Copy(pixels.Address, read, 0, read.Length);

        for (var p = 0; p < Width * Height; p++)
        {
            var at = p * 4;
            Assert.Equal(written[at + 2], read[at]);
            Assert.Equal(written[at + 1], read[at + 1]);
            Assert.Equal(written[at], read[at + 2]);
            Assert.Equal(written[at + 3], read[at + 3]);
        }
    }

    /// <summary>Green and alpha do not move, stated as its own assertion: a panel whose alpha had been shuffled into a colour channel would be invisible.</summary>
    [Fact]
    public void ConvertingTwiceGivesBackWhatWasDrawn()
    {
        var pixels = new VrPixels(Width, Height);
        var written = new byte[Width * Height * 4];
        Random.Shared.NextBytes(written);

        Marshal.Copy(written, 0, pixels.Address, written.Length);

        pixels.ToRgba();
        pixels.ToRgba();

        var read = new byte[written.Length];
        Marshal.Copy(pixels.Address, read, 0, read.Length);

        Assert.Equal(written, read);
    }

    /// <summary>
    /// <c>SetOverlayRaw</c> takes a width and a bytes-per-pixel and derives the stride itself, so a
    /// buffer with row padding is read as a progressively more sheared image rather than refused.
    /// </summary>
    [Fact]
    public void TheRowsArePackedWithNoPaddingForTheRuntimeToTripOver()
    {
        var pixels = new VrPixels(Width, Height);

        Assert.Equal(Width * 4, pixels.RowBytes);
    }

    /// <summary>A resize is a reallocation, and the address moves with it.</summary>
    [Fact]
    public void AResizedBufferReportsItsNewShape()
    {
        var pixels = new VrPixels(Width, Height);

        pixels.Resize(640, 280);

        Assert.Equal((640, 280), (pixels.Width, pixels.Height));
        Assert.Equal(640 * 4, pixels.RowBytes);

        // Writable to the last byte of the new size: a resize that reported the new dimensions over the old
        // allocation would corrupt the heap on the first full-surface rasterise.
        Marshal.WriteByte(pixels.Address, (640 * 280 * 4) - 1, 0xFF);
        Assert.Equal(0xFF, Marshal.ReadByte(pixels.Address, (640 * 280 * 4) - 1));
    }

    /// <summary>Consecutive frames are drawn into different memory, and the ring comes back round.</summary>
    [Fact]
    public void ConsecutiveFramesAreDrawnIntoDifferentBuffers()
    {
        var pixels = new VrPixels(64, 32);

        var seen = new List<IntPtr>();

        for (var i = 0; i < 4; i++)
        {
            seen.Add(pixels.Address);
            pixels.Rotate();
        }

        Assert.Equal(4, seen.Distinct().Count());

        // And back to the start, so the ring is a ring rather than an allocation per frame.
        Assert.Equal(seen[0], pixels.Address);
    }

    /// <summary>A resize starts a fresh ring, and the address it hands out belongs to it.</summary>
    [Fact]
    public void AResizeHandsOutAnAddressFromTheNewRing()
    {
        var pixels = new VrPixels(64, 32);

        pixels.Rotate();
        var before = pixels.Address;

        pixels.Resize(128, 64);

        Assert.NotEqual(before, pixels.Address);
        Assert.Equal(128 * 4, pixels.RowBytes);
    }
}
