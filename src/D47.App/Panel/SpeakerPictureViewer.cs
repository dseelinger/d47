using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>A speaker's or Codex entry's picture at full size over the whole window, put away by Close or Escape.</summary>
internal sealed class SpeakerPictureViewer : Grid
{
    private readonly OverlayLayer _layer;

    private SpeakerPictureViewer(Bitmap picture, OverlayLayer layer)
    {
        _layer = layer;

        Children.Add(new Image
        {
            Name = "ViewedPicture",
            Source = picture,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 72),
        });

        LoadoutPages.Themed(this, BackgroundProperty, ThemeManager.BgKey);

        var close = LoadoutPages.Press("Close", Dismiss);
        close.Name = "ClosePicture";
        close.MinHeight = 44;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(14);
        Children.Add(close);

        Focusable = true;

        // OverlayLayer arranges a child at the size it asks for, so the layer's size is asked for here.
        Fill();
        layer.SizeChanged += Filled;
        DetachedFromVisualTree += (_, _) => layer.SizeChanged -= Filled;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Dismiss();
                e.Handled = true;
            }
        };

        AttachedToVisualTree += (_, _) => Focus();
    }

    /// <summary>
    /// Makes <paramref name="image"/> open <paramref name="picture"/> full size when pressed. The press is handled, so
    /// the control under the image does not also act on it.
    /// </summary>
    internal static void Enlarges(Image image, SpeakerPictures pictures, string? picture)
    {
        if (picture is null)
        {
            return;
        }

        Press(image, () => Open(image, pictures, picture));
    }

    /// <summary>Makes <paramref name="image"/> open the already decoded <paramref name="picture"/> full size when pressed.</summary>
    internal static void Enlarges(Image image, Bitmap picture) => Press(image, () => Open(image, picture));

    private static void Press(Image image, Action open)
    {
        image.Cursor = new Cursor(StandardCursorType.Hand);

        image.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(image).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            open();
        };
    }

    internal static void Open(Visual from, Bitmap picture)
    {
        if (OverlayLayer.GetOverlayLayer(from) is { } layer)
        {
            layer.Children.Add(new SpeakerPictureViewer(picture, layer));
        }
    }

    internal static void Open(Visual from, SpeakerPictures pictures, string? picture)
    {
        if (OverlayLayer.GetOverlayLayer(from) is not { } layer || pictures.Find(picture) is not { } file)
        {
            return;
        }

        try
        {
            using var bytes = new MemoryStream(pictures.Files.ReadBytes(file) ?? throw new FileNotFoundException("The picture is gone.", file));
            layer.Children.Add(new SpeakerPictureViewer(new Bitmap(bytes), layer));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
        }
    }

    private void Dismiss() => _layer.Children.Remove(this);

    private void Filled(object? sender, SizeChangedEventArgs e) => Fill();

    private void Fill()
    {
        Width = _layer.Bounds.Width;
        Height = _layer.Bounds.Height;
    }
}
