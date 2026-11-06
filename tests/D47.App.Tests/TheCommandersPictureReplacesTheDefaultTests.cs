using D47.Core.Storage;
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
/// every message with that picture reads it ahead of the story's own. A file that does
/// not decode is refused and writes nothing.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheCommandersPictureReplacesTheDefaultTests
{
    private const string Picture = "ride-along.stowaway";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "d47-cast-picture");

    private readonly MemoryFileSystem _files = new();

    internal static byte[] Jpeg(int width, int height, Color colour)
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

    private PixelSize SizeOf(string file)
    {
        using var stream = _files.OpenRead(file)!;
        using var bitmap = new Bitmap(stream);
        return bitmap.PixelSize;
    }

    [AvaloniaFact]
    public void ALargeJpegIsKeptAsAPngOf1024()
    {
        var target = Path.Combine(Root, "pictures", Picture + ".png");
        using var source = new MemoryStream(Jpeg(4000, 3000, Colors.SteelBlue));

        Assert.Null(PictureImport.Save(_files, source, "big.jpg", target));

        Assert.Equal(new PixelSize(1024, 768), SizeOf(target));
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], _files.ReadBytes(target)!.Take(4));
    }

    [AvaloniaFact]
    public void ATextFileNamedPngIsRefusedAndNothingIsWritten()
    {
        var folder = Path.Combine(Root, "pictures");
        using var source = new MemoryStream("not a picture"u8.ToArray());

        Assert.NotNull(PictureImport.Save(_files, source, "notes.png", Path.Combine(folder, Picture + ".png")));

        Assert.Empty(_files.Enumerate(Root, "*", recursive: true));
    }

    [AvaloniaFact]
    public void AFileOver10MbIsRefused()
    {
        var target = Path.Combine(Root, Picture + ".png");
        using var source = new MemoryStream(new byte[PictureImport.MostBytes + 1]);

        Assert.Contains("10 MB", PictureImport.Save(_files, source, "huge.png", target), StringComparison.Ordinal);
        Assert.Null(_files.Stat(target));
    }
}
