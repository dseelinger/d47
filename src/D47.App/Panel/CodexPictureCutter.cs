using Avalonia;
using Avalonia.Media;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Cuts a codex picture from a capture.</summary>
internal static class CodexPictureCutter
{
    /// <summary>The JPEG quality a codex picture is saved at, from 0 to 100.</summary>
    public const int Quality = 90;

    /// <summary>
    /// <paramref name="source"/> decoded with every channel lifted by <paramref name="gamma"/>; null when it cannot be read.
    /// Blocks on decoding and the lift; call it on the pool.
    /// </summary>
    public static WriteableBitmap? DecodeLifted(byte[] source, double gamma)
    {
        try
        {
            using var stream = new MemoryStream(source);
            var bitmap = WriteableBitmap.Decode(stream);
            var table = PictureLift.Table(gamma);

            using (var frame = bitmap.Lock())
            {
                var row = new byte[frame.RowBytes];

                for (var y = 0; y < frame.Size.Height; y++)
                {
                    var at = frame.Address + (y * frame.RowBytes);
                    Marshal.Copy(at, row, 0, row.Length);

                    for (var x = 0; x < frame.Size.Width * 4; x += 4)
                    {
                        row[x] = table[row[x]];
                        row[x + 1] = table[row[x + 1]];
                        row[x + 2] = table[row[x + 2]];
                    }

                    Marshal.Copy(row, 0, at, row.Length);
                }
            }

            return bitmap;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The square of <paramref name="source"/>, lifted by <paramref name="gamma"/> when it has one, scaled to
    /// <see cref="CropSquare.SavedSide"/> and encoded as a JPEG; null when the capture cannot be read.
    /// Blocks on decoding and encoding; call it on the pool.
    /// </summary>
    public static byte[]? Cut(byte[] source, CropSquare square, double? gamma = null)
    {
        try
        {
            using var stream = new MemoryStream(source);
            using var decoded = gamma is { } lift ? DecodeLifted(source, lift) ?? throw new InvalidOperationException() : new Bitmap(stream);
            var fitted = square.Fit(decoded.PixelSize.Width, decoded.PixelSize.Height);
            var side = CropSquare.SavedSide;

            using var target = new RenderTargetBitmap(new PixelSize(side, side), new Vector(96, 96));

            using (var context = target.CreateDrawingContext())
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(
                    decoded,
                    new Rect(fitted.X, fitted.Y, fitted.Side, fitted.Side),
                    new Rect(0, 0, side, side));
            }

            using var jpeg = new MemoryStream();
            target.Save(jpeg, new JpegBitmapEncoderOptions { Quality = Quality });

            return jpeg.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
