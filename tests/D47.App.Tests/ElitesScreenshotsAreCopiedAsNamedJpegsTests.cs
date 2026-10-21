using System.Globalization;
using System.Text;
using D47.App.Media;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Graphics.Imaging;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Each live <c>Screenshot</c> event becomes one JPEG under <c>D47\</c>, named for its time and place, once Elite's BMP
/// is complete; Elite's BMP is never changed.
/// </summary>
public sealed class ElitesScreenshotsAreCopiedAsNamedJpegsTests
{
    private const string Folder = @"C:\Pictures\Frontier Developments\Elite Dangerous";

    private const string Target = Folder + @"\D47";

    private static readonly DateTimeOffset Taken = new(2026, 2, 10, 9, 5, 0, TimeSpan.Zero);

    private static readonly JournalLocation Nowhere = JournalLocation.Unknown;

    private readonly MemoryFileSystem _files = new();

    private int _delays;

    private TimeSpan _waited;

    private Action<int>? _onDelay;

    private ScreenshotConverter Converter() => new(
        _files,
        Folder,
        NullLogger<ScreenshotConverter>.Instance,
        stream => Encoding.ASCII.GetBytes($"jpeg of {stream.Length}"),
        delay =>
        {
            _delays++;
            _waited += delay;
            _onDelay?.Invoke(_delays);
            return Task.CompletedTask;
        });

    private static JournalEvent Event(string file, string place = """, "System":"Example System", "Body":"Example System A 1" """)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""{ "timestamp":"{{Taken.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}}", "event":"Screenshot", "Filename":"\\ED_Pictures\\{{file}}", "Width":5, "Height":3 {{place}} }""",
            NullLogger.Instance,
            out var parsed));

        return parsed!;
    }

    private static string Stamp => Taken.LocalDateTime.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);

    private static async Task Run(ScreenshotConverter converter, IReadOnlyList<JournalEvent> events, bool priming = false, bool enabled = true, JournalLocation? fallback = null)
    {
        converter.Observe(events, priming, enabled, fallback ?? Nowhere);
        await converter.Running;
    }

    private IReadOnlyList<string> Written() => _files.Enumerate(Target, "*.jpg");

    [Fact]
    public async Task ALiveScreenshotWithItsBmpPresentIsWrittenUnderD47AndTheBmpIsUnchanged()
    {
        var bmp = Bmp(5, 3);
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), bmp);

        await Run(Converter(), [Event("Screenshot_0001.bmp")]);

        Assert.Equal([Path.Combine(Target, $"{Stamp} Example System A 1.jpg")], Written());
        Assert.Equal(Encoding.ASCII.GetBytes($"jpeg of {bmp.Length}"), _files.ReadBytes(Written()[0]));
        Assert.Equal(bmp, _files.ReadBytes(Path.Combine(Folder, "Screenshot_0001.bmp")));
        Assert.Equal(0, _delays);
    }

    [Fact]
    public async Task TheSameEventOnTheFirstTickWritesNothing()
    {
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp")], priming: true);

        Assert.Empty(Written());
    }

    [Fact]
    public async Task WithConvertingOffNothingIsWritten()
    {
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp")], enabled: false);

        Assert.Empty(Written());
    }

    [Fact]
    public async Task ABmpShorterThanItsHeaderSaysIsConvertedOnceItReachesIt()
    {
        var bmp = Bmp(5, 3);
        var path = Path.Combine(Folder, "Screenshot_0001.bmp");
        _files.WriteBytes(path, bmp[..20]);
        _onDelay = count =>
        {
            if (count == 3)
            {
                _files.WriteBytes(path, bmp);
            }
        };

        await Run(Converter(), [Event("Screenshot_0001.bmp")]);

        Assert.Equal(3, _delays);
        Assert.Equal(Encoding.ASCII.GetBytes($"jpeg of {bmp.Length}"), _files.ReadBytes(Assert.Single(Written())));
    }

    [Fact]
    public async Task ABmpMissingForTheWholeWaitIsSkippedAndTheNextIsStillConverted()
    {
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0002.bmp"), Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp", """, "System":"First" """), Event("Screenshot_0002.bmp", """, "System":"Second" """)]);

        Assert.Equal(TimeSpan.FromSeconds(120), _waited);
        Assert.Equal([Path.Combine(Target, $"{Stamp} Second.jpg")], Written());
    }

    [Fact]
    public async Task TwoScreenshotsWithTheSameTimeAndPlaceTakeTheNameAndTheNextCopy()
    {
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), Bmp(5, 3));
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0002.bmp"), Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp"), Event("Screenshot_0002.bmp")]);

        Assert.Equal(
            [Path.Combine(Target, $"{Stamp} Example System A 1 (2).jpg"), Path.Combine(Target, $"{Stamp} Example System A 1.jpg")],
            Written());
    }

    [Fact]
    public async Task AnEventWithNoSystemIsNamedFromTheFallback()
    {
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp", "")], fallback: new JournalLocation("Fallback", "Fallback 2", false, null));

        Assert.Equal([Path.Combine(Target, $"{Stamp} Fallback 2.jpg")], Written());
    }

    [Fact]
    public async Task ARealBmpBecomesAJpegOfTheSameSize()
    {
        using var bmp = new MemoryStream(Bmp(5, 3));

        var jpeg = ScreenshotConverter.ToJpeg(bmp);

        var decoder = await BitmapDecoder.CreateAsync(System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(new MemoryStream(jpeg)));

        Assert.Equal(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
        Assert.Equal((5u, 3u), (decoder.PixelWidth, decoder.PixelHeight));
    }

    /// <summary>A 24-bit bottom-up BMP, its rows padded to four bytes.</summary>
    private static byte[] Bmp(int width, int height)
    {
        var stride = (width * 3 + 3) & ~3;
        var size = 54 + stride * height;
        var bytes = new byte[size];

        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.TryWriteBytes(bytes.AsSpan(2), size);
        BitConverter.TryWriteBytes(bytes.AsSpan(10), 54);
        BitConverter.TryWriteBytes(bytes.AsSpan(14), 40);
        BitConverter.TryWriteBytes(bytes.AsSpan(18), width);
        BitConverter.TryWriteBytes(bytes.AsSpan(22), height);
        BitConverter.TryWriteBytes(bytes.AsSpan(26), (short)1);
        BitConverter.TryWriteBytes(bytes.AsSpan(28), (short)24);
        BitConverter.TryWriteBytes(bytes.AsSpan(34), stride * height);

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var at = 54 + row * stride + column * 3;
                bytes[at] = (byte)(column * 40);
                bytes[at + 1] = (byte)(row * 80);
                bytes[at + 2] = 0xC0;
            }
        }

        return bytes;
    }
}
