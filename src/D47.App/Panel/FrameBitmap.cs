using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using D47.App.Media;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>Draws a decoded video picture into an image, and the caption laid over the bottom of one.</summary>
internal static class FrameBitmap
{
    /// <summary>Copies <paramref name="frame"/> into <paramref name="bitmap"/>, or into a new bitmap when its size differs, and returns the one drawn.</summary>
    public static WriteableBitmap Draw(VideoFrame frame, WriteableBitmap? bitmap)
    {
        if (bitmap is null || bitmap.PixelSize.Width != frame.Width || bitmap.PixelSize.Height != frame.Height)
        {
            bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        using var buffer = bitmap.Lock();
        var row = frame.Width * 4;

        for (var y = 0; y < frame.Height; y++)
        {
            Marshal.Copy(frame.Pixels, y * row, buffer.Address + (y * buffer.RowBytes), row);
        }

        return bitmap;
    }

    /// <summary>White Segoe UI on a scrim, centred at the bottom of the picture.</summary>
    public static (Border Band, TextBlock Text) Caption(double size, Thickness padding, Thickness margin)
    {
        var text = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeight.SemiBold,
            FontSize = size,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        var band = new Border
        {
            Background = ThemeManager.Scrim(),
            Padding = padding,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = margin,
            IsVisible = false,
            Child = text,
        };

        return (band, text);
    }
}
