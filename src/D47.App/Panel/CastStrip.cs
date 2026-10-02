using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>A row of square cast thumbnails for a story card.</summary>
internal static class CastStrip
{
    public const int Size = 44;

    private static readonly ConcurrentDictionary<(string Path, DateTime Written), Bitmap> Cache = new();

    /// <summary>The strip for <paramref name="pictures"/> as <paramref name="gender"/> meets them, or null when none is on disk.</summary>
    public static Control? For(StoryCard card, string? gender, CastPictures? pictures)
    {
        if (pictures is null)
        {
            return null;
        }

        var strip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 4) };

        foreach (var file in card.PicturesFor(gender).Select(pictures.Find).OfType<string>())
        {
            if (Thumbnail(file) is { } bitmap)
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

    private static Bitmap? Thumbnail(string file)
    {
        try
        {
            var key = (file, File.GetLastWriteTimeUtc(file));

            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            using var stream = File.OpenRead(file);
            return Cache[key] = Bitmap.DecodeToWidth(stream, Size);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
