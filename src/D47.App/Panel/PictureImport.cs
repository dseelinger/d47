using Avalonia;
using Avalonia.Media.Imaging;

namespace D47.App.Panel;

/// <summary>Turns a picture the Commander picked into the picture kept in <c>data\pictures</c>.</summary>
internal static class PictureImport
{
    /// <summary>The largest file accepted.</summary>
    public const long MostBytes = 10 * 1024 * 1024;

    /// <summary>The most pixels on the longer side of the kept picture.</summary>
    public const int LongestSide = 1024;

    public static readonly IReadOnlyList<string> Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"];

    /// <summary>
    /// Decodes <paramref name="source"/>, scales it to at most <see cref="LongestSide"/> on the longer side and
    /// writes it to <paramref name="target"/> as a PNG. Returns the reason it was refused, or null. A refused file
    /// writes nothing. Blocks on disk and decoding; call it on the pool.
    /// </summary>
    public static string? Save(Stream source, string fileName, string target)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!Patterns.Contains("*" + extension, StringComparer.Ordinal))
        {
            return $"{fileName} is not a PNG, JPEG, BMP or WebP picture.";
        }

        using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            bytes.Write(buffer, 0, read);

            if (bytes.Length > MostBytes)
            {
                return $"{fileName} is over 10 MB.";
            }
        }

        bytes.Position = 0;
        Bitmap decoded;

        try
        {
            decoded = new Bitmap(bytes);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"{fileName} could not be read as a picture.";
        }

        using (decoded)
        {
            var size = decoded.PixelSize;
            var longer = Math.Max(size.Width, size.Height);

            if (longer <= 0)
            {
                return $"{fileName} could not be read as a picture.";
            }

            using var scaled = longer > LongestSide
                ? decoded.CreateScaledBitmap(
                    new PixelSize(
                        Math.Max(1, (int)Math.Round((double)size.Width * LongestSide / longer)),
                        Math.Max(1, (int)Math.Round((double)size.Height * LongestSide / longer))),
                    BitmapInterpolationMode.HighQuality)
                : null;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var partial = target + ".part";
            (scaled ?? decoded).Save(partial, new PngBitmapEncoderOptions());
            File.Move(partial, target, overwrite: true);
        }

        return null;
    }
}
