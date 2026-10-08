using System.Runtime.InteropServices.WindowsRuntime;
using D47.App.Diagnostics;
using D47.Core.Interface;
using Windows.Graphics.Imaging;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A picture of the screen is a JPEG inside both size caps; an eye picture is its centred 80%; the input
/// trace's stills stay 960-pixel-wide PNGs.
/// </summary>
public class AScreenPictureIsAJpegWithinTheCapsTests
{
    private static readonly (byte B, byte G, byte R) Red = (0x00, 0x00, 0xFF);
    private static readonly (byte B, byte G, byte R) Blue = (0xFF, 0x00, 0x00);
    private static readonly (byte B, byte G, byte R) Slate = (0x40, 0x80, 0xC0);

    [Fact]
    public async Task AWindowPictureIsAJpegInsideBothCaps()
    {
        using var bitmap = Bitmap(3840, 2160, (_, _) => Slate);

        var picture = EliteWindowCapture.Jpeg(bitmap, ScreenPictures.FromWindow);

        Assert.Equal(ScreenPictures.FromWindow, picture.Source);
        Assert.Equal([0xFF, 0xD8], picture.Jpeg[..2]);
        Assert.InRange(Math.Max(picture.Width, picture.Height), 1, ScreenPictures.MaxLongEdge);
        Assert.InRange(picture.Width * picture.Height, 1, ScreenPictures.MaxPixels);

        var decoder = await Decode(picture.Jpeg);

        Assert.Equal(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
        Assert.Equal((uint)picture.Width, decoder.PixelWidth);
        Assert.Equal((uint)picture.Height, decoder.PixelHeight);
    }

    [Fact]
    public async Task AnEyePictureLeavesOutTheOuterTenthOnEachSide()
    {
        const int Size = 2000;
        var border = Size / 10;

        var pixels = Pixels(Size, Size, (x, y) =>
            x < border || y < border || x >= Size - border || y >= Size - border
                ? Red
                : Blue);

        var (cropped, columns, rows) = HeadsetEyeCapture.Crop(pixels, Size, Size);

        Assert.Equal((Size * 4 / 5, Size * 4 / 5), (columns, rows));

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            cropped.AsBuffer(), BitmapPixelFormat.Bgra8, columns, rows, BitmapAlphaMode.Premultiplied);

        var picture = EliteWindowCapture.Jpeg(bitmap, ScreenPictures.FromHeadset);

        Assert.InRange(Math.Max(picture.Width, picture.Height), 1, ScreenPictures.MaxLongEdge);
        Assert.InRange(picture.Width * picture.Height, 1, ScreenPictures.MaxPixels);

        var decoded = (await (await Decode(picture.Jpeg)).GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage))
            .DetachPixelData();

        var red = 0;

        for (var at = 0; at < decoded.Length; at += 4)
        {
            if (decoded[at + 2] > 0x80 && decoded[at] < 0x80)
            {
                red++;
            }
        }

        Assert.Equal(0, red);
    }

    [Fact]
    public async Task ATraceStillIsStillA960PixelPng()
    {
        using var bitmap = Bitmap(1920, 1080, (_, _) => Slate);
        var path = Path.Combine(Path.GetTempPath(), $"d47-still-{Guid.NewGuid():N}.png");

        try
        {
            EliteWindowCapture.Save(bitmap, path, InputTraceWriter.StillWidth);

            var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            var decoder = await Decode(bytes);

            Assert.Equal(BitmapDecoder.PngDecoderId, decoder.DecoderInformation.CodecId);
            Assert.Equal((uint)InputTraceWriter.StillWidth, decoder.PixelWidth);
            Assert.Equal(540u, decoder.PixelHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SoftwareBitmap Bitmap(int width, int height, Func<int, int, (byte B, byte G, byte R)> colour) =>
        SoftwareBitmap.CreateCopyFromBuffer(
            Pixels(width, height, colour).AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);

    private static byte[] Pixels(int width, int height, Func<int, int, (byte B, byte G, byte R)> colour)
    {
        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (b, g, r) = colour(x, y);
                var at = ((y * width) + x) * 4;

                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (b, g, r, 0xFF);
            }
        }

        return pixels;
    }

    private static async Task<BitmapDecoder> Decode(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);

        return await BitmapDecoder.CreateAsync(System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(stream));
    }
}
