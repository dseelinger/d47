using System.Globalization;
using System.Text;
using D47.App.Media;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Graphics.Imaging;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Each live <c>Screenshot</c> event becomes one JPEG under <c>D47\</c>, named for its time and place, once Elite's BMP
/// is complete; Elite's BMP is sent to the Recycle Bin afterwards only when the screenshot was queued with the delete flag
/// and the BMP is unchanged since it was read.
/// </summary>
public sealed class ElitesScreenshotsAreCopiedAsNamedJpegsTests
{
    private const string Folder = @"C:\Pictures\Frontier Developments\Elite Dangerous";

    private const string Target = Folder;

    private static readonly DateTimeOffset Taken = new(2026, 2, 10, 9, 5, 0, TimeSpan.Zero);

    private static readonly JournalLocation Nowhere = JournalLocation.Unknown;

    private readonly MemoryFileSystem _files = new();

    private int _delays;

    private TimeSpan _waited;

    private Action<int>? _onDelay;

    private readonly List<string> _recycled = [];

    private readonly RecordingLogger<ScreenshotConverter> _log = new();

    private Func<string, bool?>? _recycle;

    private Func<Stream, byte[]> _encode = stream => Encoding.ASCII.GetBytes($"jpeg of {stream.Length}");

    private ScreenshotConverter Converter() => new(
        _files,
        Folder,
        _log,
        path =>
        {
            _recycled.Add(path);

            if (_recycle?.Invoke(path) is { } result)
            {
                return result;
            }

            _files.Delete(path);
            return true;
        },
        stream => _encode(stream),
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

    private static async Task Run(ScreenshotConverter converter, IReadOnlyList<JournalEvent> events, bool priming = false, bool enabled = true, JournalLocation? fallback = null, CommanderGameState? state = null, bool deleteOriginal = false)
    {
        converter.Observe(events, priming, enabled, deleteOriginal, fallback ?? Nowhere, state);
        await converter.Running;
    }

    private IReadOnlyList<string> Written() => _files.Enumerate(Target, "*.jpg");

    [Fact]
    public async Task ALiveScreenshotWithItsBmpPresentIsWrittenInTheFolderGivenAndTheBmpIsUnchanged()
    {
        var bmp = Bmp(5, 3);
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), bmp);

        await Run(Converter(), [Event("Screenshot_0001.bmp")]);

        Assert.Equal([Path.Combine(Target, $"{Stamp} Example System A 1.jpg")], Written());
        Assert.Equal(Encoding.ASCII.GetBytes($"jpeg of {bmp.Length}"), _files.ReadBytes(Written()[0]));
        Assert.Equal(bmp, _files.ReadBytes(Path.Combine(Folder, "Screenshot_0001.bmp")));
        Assert.Equal(0, _delays);
        Assert.Equal(Folder, Converter().Target);
        Assert.Equal(Folder, Path.GetDirectoryName(Written()[0]));
        Assert.NotNull(_files.ReadText(Path.Combine(Folder, $"{Stamp} Example System A 1.json")));
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
    public async Task EachJpegHasANoteOfItsPlaceTheCodexEntriesBeforeItAndItsBmp()
    {
        var store = new GameStateStore();

        foreach (var json in new[]
        {
            """{"timestamp":"2026-02-10T08:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-02-10T08:00:01Z","event":"LoadGame","Credits":1}""",
            """{"timestamp":"2026-02-10T08:00:02Z","event":"Location","StarSystem":"Example System","SystemAddress":42,"StarPos":[0,0,0],"Body":"Example System A 1","BodyID":4}""",
            """{"timestamp":"2026-02-10T08:30:00Z","event":"CodexEntry","EntryID":1,"Name":"$Codex_Ent_1;","Name_Localised":"Too early","Region":"$Codex_RegionName_18;","System":"Example System","SystemAddress":42,"BodyID":4}""",
            """{"timestamp":"2026-02-10T09:00:00Z","event":"CodexEntry","EntryID":2,"Name":"$Codex_Ent_2;","Name_Localised":"Logged","Region":"$Codex_RegionName_18;","System":"Example System","SystemAddress":42,"BodyID":4}""",
        })
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), Bmp(5, 3));
        var stat = _files.Stat(Path.Combine(Folder, "Screenshot_0001.bmp"));

        await Run(Converter(), [Event("Screenshot_0001.bmp")], state: store.Active);

        var note = PictureNote.Parse(_files.ReadText(Path.Combine(Target, $"{Stamp} Example System A 1.json")));

        Assert.NotNull(note);
        Assert.Equal((Taken, ScreenPictures.FromScreenshot, 5, 3), (note.At, note.Source, note.Width, note.Height));
        Assert.Equal(("Example System", 42L, "A 1"), (note.System, note.SystemAddress, note.Body));
        Assert.Equal("Screenshot_0001.bmp", note.Bmp);
        Assert.NotNull(stat);
        Assert.Equal((stat.Value.Length, new DateTimeOffset(stat.Value.Written.ToUniversalTime())), (note.BmpLength, note.BmpWritten));
        Assert.Equal(2, Assert.Single(note.CodexEntries).EntryId);
    }

    private PictureNote NoteOf(string name) => PictureNote.Parse(_files.ReadText(Path.Combine(Target, name)))!;

    [Fact]
    public async Task WithTheDeleteFlagOnTheBmpIsRecycledAfterItsJpegAndTheNoteNamesNoBmp()
    {
        var path = Path.Combine(Folder, "Screenshot_0001.bmp");
        _files.WriteBytes(path, Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp")], deleteOriginal: true);

        var note = NoteOf($"{Stamp} Example System A 1.json");

        Assert.Single(Written());
        Assert.Equal([path], _recycled);
        Assert.Null(_files.Stat(path));
        Assert.Equal((null, null, null), (note.Bmp, note.BmpLength, note.BmpWritten));
    }

    [Fact]
    public async Task WithTheDeleteFlagOffTheBmpIsKeptAndTheNoteNamesIt()
    {
        var path = Path.Combine(Folder, "Screenshot_0001.bmp");
        _files.WriteBytes(path, Bmp(5, 3));

        await Run(Converter(), [Event("Screenshot_0001.bmp")]);

        var note = NoteOf($"{Stamp} Example System A 1.json");

        Assert.Single(Written());
        Assert.Empty(_recycled);
        Assert.NotNull(_files.Stat(path));
        Assert.Equal("Screenshot_0001.bmp", note.Bmp);
        Assert.NotNull(note.BmpLength);
        Assert.NotNull(note.BmpWritten);
    }

    [Fact]
    public async Task ABmpThatNeverArrivesIsNotRecycled()
    {
        await Run(Converter(), [Event("Screenshot_0001.bmp")], deleteOriginal: true);

        Assert.Empty(Written());
        Assert.Empty(_recycled);
    }

    [Fact]
    public async Task AnEncoderThatThrowsRecyclesNothing()
    {
        var path = Path.Combine(Folder, "Screenshot_0001.bmp");
        _files.WriteBytes(path, Bmp(5, 3));
        _encode = _ => throw new InvalidOperationException("not an image");

        await Run(Converter(), [Event("Screenshot_0001.bmp")], deleteOriginal: true);

        Assert.Empty(Written());
        Assert.Empty(_recycled);
        Assert.NotNull(_files.Stat(path));
    }

    [Fact]
    public async Task ABmpThatChangedAfterItWasReadIsKeptWithOneLogLineAndItsJpegAndNote()
    {
        var path = Path.Combine(Folder, "Screenshot_0001.bmp");
        _files.WriteBytes(path, Bmp(5, 3));
        _encode = stream =>
        {
            var jpeg = Encoding.ASCII.GetBytes($"jpeg of {stream.Length}");
            _files.WriteBytes(path, Bmp(6, 3));
            return jpeg;
        };

        await Run(Converter(), [Event("Screenshot_0001.bmp")], deleteOriginal: true);

        var note = NoteOf($"{Stamp} Example System A 1.json");

        Assert.Single(Written());
        Assert.Empty(_recycled);
        Assert.Single(_log.Lines);
        Assert.Equal(("Screenshot_0001.bmp", (long)Bmp(5, 3).Length), (note.Bmp, note.BmpLength));
        Assert.NotNull(note.BmpWritten);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARecycleThatFailsKeepsTheJpegAndTheNoteAndTheNextScreenshotStillConverts(bool throws)
    {
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0001.bmp"), Bmp(5, 3));
        _files.WriteBytes(Path.Combine(Folder, "Screenshot_0002.bmp"), Bmp(5, 3));
        _recycle = path => path.EndsWith("0001.bmp", StringComparison.Ordinal)
            ? throws ? throw new IOException("in use") : (bool?)false
            : null;

        await Run(
            Converter(),
            [Event("Screenshot_0001.bmp", ", \"System\":\"First\" "), Event("Screenshot_0002.bmp", ", \"System\":\"Second\" ")],
            deleteOriginal: true);

        var first = NoteOf($"{Stamp} First.json");
        var second = NoteOf($"{Stamp} Second.json");

        Assert.Equal(2, Written().Count);
        Assert.Equal(2, _recycled.Count);
        Assert.Single(_log.Lines);
        Assert.Contains("Recycle Bin", _log.Lines[0], StringComparison.Ordinal);
        Assert.Equal("Screenshot_0001.bmp", first.Bmp);
        Assert.NotNull(first.BmpLength);
        Assert.NotNull(first.BmpWritten);
        Assert.Null(second.Bmp);
        Assert.Null(_files.Stat(Path.Combine(Folder, "Screenshot_0002.bmp")));
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
