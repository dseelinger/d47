using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>A capture scaled to the size the Crop page shows it at, which a gamma is applied to on each press.</summary>
internal sealed class CodexCropPreview
{
    private readonly byte[] _pixels;
    private readonly PixelSize _size;
    private readonly PixelFormat _format;

    private CodexCropPreview(int width, int height, byte[] pixels, PixelSize size, PixelFormat format)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
        _size = size;
        _format = format;
    }

    /// <summary>The capture's size in pixels.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// The preview of <paramref name="source"/>; null when it cannot be read.
    /// Blocks on decoding; call it on the pool.
    /// </summary>
    public static CodexCropPreview? Create(byte[] source)
    {
        try
        {
            PixelSize full;

            using (var stream = new MemoryStream(source))
            using (var whole = new Bitmap(stream))
            {
                full = whole.PixelSize;
            }

            var shown = CropSurface.ShownSize(full.Width, full.Height);

            using var again = new MemoryStream(source);
            using var small = Bitmap.DecodeToWidth(again, shown.Width, BitmapInterpolationMode.HighQuality);
            var size = small.PixelSize;
            var format = small.Format ?? PixelFormats.Bgra8888;
            var stride = size.Width * 4;
            var pixels = new byte[stride * size.Height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

            try
            {
                small.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                handle.Free();
            }

            return new CodexCropPreview(full.Width, full.Height, pixels, size, format);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The preview with every channel lifted by <paramref name="gamma"/>; unchanged when there is none.</summary>
    public WriteableBitmap At(double? gamma)
    {
        var bitmap = new WriteableBitmap(_size, new Vector(96, 96), _format, AlphaFormat.Premul);
        var table = gamma is { } lift ? PictureLift.Table(lift) : null;

        using var frame = bitmap.Lock();
        var row = new byte[_size.Width * 4];

        for (var y = 0; y < _size.Height; y++)
        {
            Buffer.BlockCopy(_pixels, y * row.Length, row, 0, row.Length);

            if (table is not null)
            {
                for (var x = 0; x < row.Length; x += 4)
                {
                    row[x] = table[row[x]];
                    row[x + 1] = table[row[x + 1]];
                    row[x + 2] = table[row[x + 2]];
                }
            }

            Marshal.Copy(row, 0, frame.Address + (y * frame.RowBytes), row.Length);
        }

        return bitmap;
    }
}
