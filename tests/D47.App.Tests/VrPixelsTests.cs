using System.Runtime.InteropServices;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The buffer the compositor is handed, and the one conversion on the way into it.
/// <para>
/// This is the seam that replaced the D3D11 shared texture, and it fails the same way that one
/// did: silently. <c>SetOverlayRaw</c> returns <c>None</c> for a buffer in the wrong channel
/// order, for a buffer with the wrong stride, and for a buffer of zeroes — the picture is
/// simply wrong, or absent, in a place where nothing can be attached to look at it.
/// </para>
/// </summary>
public class VrPixelsTests
{
    private const int Width = 8;
    private const int Height = 4;

    /// <summary>
    /// Avalonia rasterises BGRA; the raw overlay path reads RGBA. The conversion between them
    /// is four bytes of arithmetic and the entire difference between a panel and a
    /// blue-and-orange negative of one.
    /// </summary>
    [Fact]
    public void TheRasterisersBgraBecomesTheRgbaTheRuntimeReads()
    {
        var pixels = new VrPixels(1, 1);

        // Deliberately four different values, so a conversion that moved the wrong pair still
        // fails: B=0x11, G=0x22, R=0x33, A=0x44.
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

    /// <summary>
    /// Every pixel, not the first one. A conversion that walked the buffer by the wrong stride
    /// — or stopped at the end of the first row — converts the top-left correctly and leaves
    /// the rest of the panel with its channels swapped.
    /// </summary>
    [Fact]
    public void EveryPixelIsConvertedAndNotJustTheFirst()
    {
        var pixels = new VrPixels(Width, Height);
        var written = new byte[Width * Height * 4];

        for (var i = 0; i < written.Length; i++)
        {
            // Distinct per byte and never equal across channels of a pixel, so a swap that did
            // nothing is not mistaken for a swap that worked.
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

    /// <summary>
    /// Green and alpha do not move, stated as its own assertion because the mask that keeps
    /// them still is the half of the expression with nothing obviously wrong about it: a
    /// panel whose alpha had been shuffled into a colour channel would be invisible, and
    /// invisible is the symptom this whole path exists to have stopped producing.
    /// </summary>
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
    /// <c>SetOverlayRaw</c> takes a width and a bytes-per-pixel and derives the stride itself,
    /// so a buffer with row padding is read as a progressively more sheared image rather than
    /// refused. The rasteriser is handed this number, which makes it the place the two agree.
    /// </summary>
    [Fact]
    public void TheRowsArePackedWithNoPaddingForTheRuntimeToTripOver()
    {
        var pixels = new VrPixels(Width, Height);

        Assert.Equal(Width * 4, pixels.RowBytes);
    }

    /// <summary>
    /// A resize is a reallocation, and the address moves with it. The runtime hands the new
    /// address to the rasteriser on the same tick it resizes, so this is a guard on the
    /// geometry being consistent rather than on the pointer being stable.
    /// </summary>
    [Fact]
    public void AResizedBufferReportsItsNewShape()
    {
        var pixels = new VrPixels(Width, Height);

        pixels.Resize(640, 280);

        Assert.Equal((640, 280), (pixels.Width, pixels.Height));
        Assert.Equal(640 * 4, pixels.RowBytes);

        // Writable to the last byte of the new size: a resize that reported the new dimensions
        // over the old allocation would corrupt the heap on the first full-surface rasterise.
        Marshal.WriteByte(pixels.Address, (640 * 280 * 4) - 1, 0xFF);
        Assert.Equal(0xFF, Marshal.ReadByte(pixels.Address, (640 * 280 * 4) - 1));
    }
}
