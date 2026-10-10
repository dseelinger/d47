using Avalonia.Media.Imaging;
using D47.Core.Interface;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// The Conversation page's speaker pictures, each file decoded once at <see cref="Size"/> and kept by path and
/// last-write time. A file that fails to decode yields null and is logged once.
/// </summary>
public sealed class SpeakerPortraits(SpeakerPictures pictures, ILogger? logger = null)
{
    /// <summary>The side of the square a picture is drawn in.</summary>
    public const int Size = 44;

    private readonly Dictionary<(string Path, DateTime Written), Bitmap?> _decoded = [];

    /// <summary>The pictures these bitmaps are read from.</summary>
    public SpeakerPictures Pictures => pictures;

    /// <summary>How many files have been decoded.</summary>
    public int Decodes { get; private set; }

    /// <summary>The bitmap for <paramref name="picture"/>, or null when it has no file or the file does not decode.</summary>
    public Bitmap? For(string? picture)
    {
        if (pictures.Find(picture) is not { } file)
        {
            return null;
        }

        if (pictures.Files.Stat(file) is not { } state)
        {
            return null;
        }

        var key = (file, state.Written);

        if (_decoded.TryGetValue(key, out var cached))
        {
            return cached;
        }

        Decodes++;

        try
        {
            using var stream = pictures.Files.OpenRead(file) ?? throw new FileNotFoundException("The picture is gone.", file);
            return _decoded[key] = Bitmap.DecodeToWidth(stream, Size, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex)
        {
            logger?.LogWarning("The speaker picture {File} did not decode: {Reason}", file, ex.Message);
            return _decoded[key] = null;
        }
    }
}
