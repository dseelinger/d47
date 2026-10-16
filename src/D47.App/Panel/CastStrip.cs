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

/// <summary>The cast pictures on a story card: the first large, any others as a column of small squares beside it.</summary>
internal static class CastStrip
{
    public const int Size = 44;

    /// <summary>The decode width of the large picture.</summary>
    public const int LargeSize = 480;

    private static readonly ConcurrentDictionary<(IFileSystem Files, string Path, DateTime Written, int Width), Bitmap> Cache = new();

    /// <summary>The pictures for <paramref name="card"/> as <paramref name="gender"/> meets them, or null when none is on disk.</summary>
    public static Control? For(StoryCard card, string? gender, SpeakerPictures? pictures)
    {
        if (pictures is null)
        {
            return null;
        }

        var area = new Pictures { ClipToBounds = true };
        var large = true;

        foreach (var file in card.PicturesFor(gender).Select(pictures.Find).OfType<string>())
        {
            if (Thumbnail(pictures.Files, file, large ? LargeSize : Size) is { } bitmap)
            {
                area.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill });
                large = false;
            }
        }

        if (area.Children.Count == 0)
        {
            return null;
        }

        AutomationProperties.SetName(area, "Cast");
        return area;
    }

    /// <summary>The first child as a square across the width, less the column of 44px squares beside it when there are more.</summary>
    private sealed class Pictures : Avalonia.Controls.Panel
    {
        private const double Gap = 2;

        private double LargeSide(double width) =>
            Children.Count > 1 ? Math.Max(0, width - Size - Gap) : width;

        private double ColumnHeight => Children.Count > 1 ? ((Children.Count - 1) * (Size + Gap)) - Gap : 0;

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? LargeSize : availableSize.Width;
            var side = LargeSide(width);

            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Measure(i == 0 ? new Size(side, side) : new Size(Size, Size));
            }

            return new Size(width, Math.Max(side, ColumnHeight));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var side = LargeSide(finalSize.Width);

            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Arrange(i == 0
                    ? new Rect(0, 0, side, side)
                    : new Rect(side + Gap, (i - 1) * (Size + Gap), Size, Size));
            }

            return finalSize;
        }
    }

    private static Bitmap? Thumbnail(IFileSystem files, string file, int width)
    {
        try
        {
            if (files.Stat(file) is not { } state)
            {
                return null;
            }

            var key = (files, file, state.Written, width);

            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            using var stream = files.OpenRead(file);
            return stream is null ? null : Cache[key] = Bitmap.DecodeToWidth(stream, width);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
