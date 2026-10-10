using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.Core.Stories;
using D47.Core.Interface;
using D47.Core.Storage;

namespace D47.App.Panel;

/// <summary>A row of square cast thumbnails for a story card.</summary>
internal static class CastStrip
{
    public const int Size = 44;

    private static readonly ConcurrentDictionary<(IFileSystem Files, string Path, DateTime Written), Bitmap> Cache = new();

    /// <summary>The strip for <paramref name="pictures"/> as <paramref name="gender"/> meets them, or null when none is on disk.</summary>
    public static Control? For(StoryCard card, string? gender, SpeakerPictures? pictures)
    {
        if (pictures is null)
        {
            return null;
        }

        var strip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 4) };

        foreach (var file in card.PicturesFor(gender).Select(pictures.Find).OfType<string>())
        {
            if (Thumbnail(pictures.Files, file) is { } bitmap)
            {
                strip.Children.Add(new Image { Source = bitmap, Width = Size, Height = Size, Stretch = Stretch.UniformToFill });
            }
        }

        if (strip.Children.Count == 0)
        {
            return null;
        }

        AutomationProperties.SetName(strip, "Cast");
        return strip;
    }

    private static Bitmap? Thumbnail(IFileSystem files, string file)
    {
        try
        {
            if (files.Stat(file) is not { } state)
            {
                return null;
            }

            var key = (files, file, state.Written);

            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            using var stream = files.OpenRead(file);
            return stream is null ? null : Cache[key] = Bitmap.DecodeToWidth(stream, Size);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
