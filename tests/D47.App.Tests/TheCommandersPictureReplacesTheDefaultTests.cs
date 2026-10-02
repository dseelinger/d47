using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A picture the Commander picks is scaled to 1024 px on its longer side and kept as a PNG in data\pictures, where
/// every message with that picture reads it ahead of the story's own; Use the default deletes it. A file that does
/// not decode is refused and writes nothing.
/// </summary>
public sealed class TheCommandersPictureReplacesTheDefaultTests
{
    private const string Picture = "ride-along.stowaway";

    private static byte[] Jpeg(int width, int height, Color colour)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var row = new byte[buffer.RowBytes];

            for (var x = 0; x < width; x++)
            {
                row[(x * 4) + 0] = colour.B;
                row[(x * 4) + 1] = colour.G;
                row[(x * 4) + 2] = colour.R;
                row[(x * 4) + 3] = 255;
            }

            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, new JpegBitmapEncoderOptions());
        return stream.ToArray();
    }

    private static PixelSize SizeOf(string file)
    {
        using var bitmap = new Bitmap(file);
        return bitmap.PixelSize;
    }

    [AvaloniaFact]
    public void ALargeJpegIsKeptAsAPngOf1024()
    {
        var target = Path.Combine(TempFolders.Create("d47-cast-picture"), "pictures", Picture + ".png");
        using var source = new MemoryStream(Jpeg(4000, 3000, Colors.SteelBlue));

        Assert.Null(CastPictureImport.Save(source, "big.jpg", target));

        Assert.Equal(new PixelSize(1024, 768), SizeOf(target));
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], File.ReadAllBytes(target).Take(4));
    }

    [AvaloniaFact]
    public void ATextFileNamedPngIsRefusedAndNothingIsWritten()
    {
        var folder = Path.Combine(TempFolders.Create("d47-cast-picture"), "pictures");
        using var source = new MemoryStream("not a picture"u8.ToArray());

        Assert.NotNull(CastPictureImport.Save(source, "notes.png", Path.Combine(folder, Picture + ".png")));

        Assert.False(Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any());
    }

    [AvaloniaFact]
    public void AFileOver10MbIsRefused()
    {
        var target = Path.Combine(TempFolders.Create("d47-cast-picture"), Picture + ".png");
        using var source = new MemoryStream(new byte[CastPictureImport.MostBytes + 1]);

        Assert.Contains("10 MB", CastPictureImport.Save(source, "huge.png", target), StringComparison.Ordinal);
        Assert.False(File.Exists(target));
    }

    [AvaloniaFact]
    public void TheMessageShowsTheCommandersPictureUntilTheDefaultIsChosen()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(TestSurface.Settings().Current.Ui.Theme);

        var paths = new AppPaths(TempFolders.Create("d47-cast-picture"));
        Directory.CreateDirectory(paths.Stories);
        var pictures = new CastPictures(paths);
        File.WriteAllBytes(pictures.Default(Picture), Jpeg(300, 300, Colors.DarkOrange));

        var messages = new MessageStore(Path.Combine(paths.Data, "messages.json"), NullLogger<MessageStore>.Instance);
        var earlier = messages.Post("Juno", "Ride Along", "You did not see me.", DateTimeOffset.Now.AddDays(-1), picture: Picture);
        var later = messages.Post("Juno", "Ride Along", "Still here.", DateTimeOffset.Now, picture: Picture);

        using (var chosen = new MemoryStream(Jpeg(2000, 1000, Colors.SteelBlue)))
        {
            Assert.Null(CastPictureImport.Save(chosen, "mine.jpg", pictures.Chosen(Picture)));
        }

        var view = new MessagesView(messages, new PanelNavigator(), AdventureFixture.Surface(paths) with { Pictures = pictures });

        Assert.Equal(new PixelSize(1024, 512), Shown(view, earlier).PixelSize);
        var page = view.Build(new NavCrumb(MessagesView.ReadPrefix + later.Key, "Ride Along"))!;
        Assert.Equal(new PixelSize(1024, 512), ShownIn(page).PixelSize);

        var window = new Window
        {
            Content = page,
            Width = 640,
            Height = 600,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(Path.Combine(TestSurface.CaptureDirectory, "message-cast-picture-chosen.png"), new PngBitmapEncoderOptions());
        }

        var image = page.GetVisualDescendants().OfType<Image>().Single();
        Assert.True(image.Bounds.Width <= 240);

        page.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Use the default"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(File.Exists(pictures.Chosen(Picture)));
        Assert.Equal(new PixelSize(300, 300), ShownIn(page).PixelSize);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(Path.Combine(TestSurface.CaptureDirectory, "message-cast-picture-default.png"), new PngBitmapEncoderOptions());
        }

        window.Close();
        Assert.Equal(new PixelSize(300, 300), Shown(view, earlier).PixelSize);
    }

    private static Bitmap Shown(MessagesView view, D47Message message) =>
        ShownIn(view.Build(new NavCrumb(MessagesView.ReadPrefix + message.Key, message.Subject))!);

    private static Bitmap ShownIn(Control page) =>
        (Bitmap)page.GetLogicalDescendants().OfType<Image>().Single().Source!;
}
