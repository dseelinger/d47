using Avalonia;
using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Interface;

namespace D47.App.Tests;

/// <summary>A Conversation page with speaker pictures, over a data folder and a build folder of its own.</summary>
internal sealed class SpeakerPictureFixture
{
    public SpeakerPictureFixture(string name)
    {
        var root = TempFolders.Create(name);
        Paths = new AppPaths(root, Path.Combine(root, "build"));
        Paths.EnsureCreated();
        Directory.CreateDirectory(Paths.ShippedPortraits);
        Portraits = new SpeakerPortraits(new SpeakerPictures(new DiskFileSystem(), Paths));
    }

    public AppPaths Paths { get; }

    public SpeakerPortraits Portraits { get; }

    public PanelViewModel Model { get; } = new();

    /// <summary>Writes a shipped portrait, <c>portraits\&lt;picture&gt;.jpg</c>.</summary>
    public void Ship(string picture, Color colour) =>
        File.WriteAllBytes(Path.Combine(Paths.ShippedPortraits, picture + ".jpg"), Encoded(colour, jpeg: true));

    /// <summary>Writes the Commander's own file, <c>data\pictures\&lt;picture&gt;.png</c>.</summary>
    public void Choose(string picture, Color colour)
    {
        Directory.CreateDirectory(Paths.Pictures);
        File.WriteAllBytes(Path.Combine(Paths.Pictures, picture + ".png"), Encoded(colour, jpeg: false));
    }

    public (Window Window, PanelView Panel) Open(double width = 900, double height = 700)
    {
        var panel = new PanelView { DataContext = Model };
        panel.EnableSpeakerPictures(Portraits);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        var bounds = new Rect(0, 0, width, height);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    /// <summary>Every speaker picture drawn on the page.</summary>
    public static IReadOnlyList<Image> Pictures(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").GetVisualDescendants().OfType<Image>().Where(image => image.Name == "SpeakerPicture")];

    private static byte[] Encoded(Color colour, bool jpeg)
    {
        const int size = 64;

        using var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var row = new byte[buffer.RowBytes];

            for (var x = 0; x < size; x++)
            {
                row[(x * 4) + 0] = colour.B;
                row[(x * 4) + 1] = colour.G;
                row[(x * 4) + 2] = colour.R;
                row[(x * 4) + 3] = 255;
            }

            for (var y = 0; y < size; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
            }
        }

        using var stream = new MemoryStream();

        if (jpeg)
        {
            bitmap.Save(stream, new JpegBitmapEncoderOptions());
        }
        else
        {
            bitmap.Save(stream, new PngBitmapEncoderOptions());
        }

        return stream.ToArray();
    }
}
