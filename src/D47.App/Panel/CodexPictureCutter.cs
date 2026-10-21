using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Cuts a codex picture from a capture.</summary>
internal static class CodexPictureCutter
{
    /// <summary>The JPEG quality a codex picture is saved at, from 0 to 100.</summary>
    public const int Quality = 90;

    /// <summary>
    /// The square of <paramref name="source"/> scaled to <see cref="CropSquare.SavedSide"/> and encoded as a JPEG;
    /// null when the capture cannot be read. Blocks on decoding and encoding; call it on the pool.
    /// </summary>
    public static byte[]? Cut(byte[] source, CropSquare square)
    {
        try
        {
            using var stream = new MemoryStream(source);
            using var decoded = new Bitmap(stream);
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
