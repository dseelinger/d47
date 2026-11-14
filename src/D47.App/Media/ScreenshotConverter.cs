using System.Collections.Concurrent;
using D47.App.Diagnostics;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;

namespace D47.App.Media;

/// <summary>
/// Copies each Elite screenshot to a named JPEG, with a <see cref="PictureNote"/> beside it, in
/// Elite's pictures folder, one at a time on the pool. Elite's BMP is read, and sent to the Recycle Bin through
/// <c>recycle</c> once its JPEG is saved when the screenshot was queued with <c>deleteOriginal</c>.
/// </summary>
public sealed class ScreenshotConverter(
    IFileSystem files,
    string folder,
    ILogger<ScreenshotConverter> logger,
    Func<string, bool> recycle,
    Func<Stream, byte[]> encode,
    Func<TimeSpan, Task> delay)
{
    /// <summary>How long a BMP has to reach its full length before it is skipped.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(120);

    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    private readonly ConcurrentQueue<(EliteScreenshot Shot, PictureNote Note, bool DeleteOriginal)> _queue = new();

    private int _running;

    public ScreenshotConverter(IFileSystem files, string folder, ILogger<ScreenshotConverter> logger, Func<string, bool> recycle)
        : this(files, folder, logger, recycle, ToJpeg, delay => Task.Delay(delay))
    {
    }

    /// <summary>The folder the JPEGs are written to.</summary>
    public string Target => folder;

    /// <summary>The pool's current run, for a test to wait on; completed when nothing is queued.</summary>
    internal Task Running { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// On the tick: queues the tick's screenshots, each with its note from <paramref name="state"/>, and starts the pool
    /// when it is idle. Reads no file. <paramref name="state"/> is null while Elite runs a Commander other than the one shown.
    /// </summary>
    public void Observe(IReadOnlyList<JournalEvent> events, bool priming, bool enabled, bool deleteOriginal, JournalLocation fallback, CommanderGameState? state = null)
    {
        if (priming || !enabled)
        {
            return;
        }

        foreach (var journalEvent in events)
        {
            if (EliteScreenshot.TryParse(journalEvent, fallback) is { } shot)
            {
                _queue.Enqueue((shot, ScreenCapability.ScreenshotNote(shot, journalEvent.Timestamp, state), deleteOriginal));
            }
        }

        if (!_queue.IsEmpty && Interlocked.CompareExchange(ref _running, 1, 0) == 0)
        {
            Running = Task.Run(DrainAsync);
        }
    }

    private async Task DrainAsync()
    {
        do
        {
            while (_queue.TryDequeue(out var queued))
            {
                try
                {
                    await ConvertAsync(queued.Shot, queued.Note, queued.DeleteOriginal);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "The screenshot {File} could not be converted", queued.Shot.FileName);
                }
            }

            Volatile.Write(ref _running, 0);
        }
        while (!_queue.IsEmpty && Interlocked.CompareExchange(ref _running, 1, 0) == 0);
    }

    private async Task ConvertAsync(EliteScreenshot shot, PictureNote note, bool deleteOriginal)
    {
        var source = shot.SourcePath(folder);

        if (!await Arrived(source))
        {
            logger.LogInformation("The screenshot {File} did not arrive within {Seconds}s and was skipped", shot.FileName, Patience.TotalSeconds);
            return;
        }

        if (files.Stat(source) is not { } state)
        {
            logger.LogInformation("The screenshot {File} was gone before it was read and was skipped", shot.FileName);
            return;
        }

        note = note with { BmpLength = state.Length, BmpWritten = new DateTimeOffset(state.Written.ToUniversalTime()) };

        byte[] jpeg;

        using (var stream = files.OpenRead(source))
        {
            if (stream is null)
            {
                logger.LogInformation("The screenshot {File} was gone before it was read and was skipped", shot.FileName);
                return;
            }

            jpeg = encode(stream);
        }

        var local = note.At.LocalDateTime;
        var copy = 1;

        while (files.Stat(Path.Combine(Target, shot.Name(local, copy))) is not null)
        {
            copy++;
        }

        var written = Path.Combine(Target, shot.Name(local, copy));

        files.WriteBytes(written, jpeg);

        if (deleteOriginal && Recycled(source, shot.FileName, state))
        {
            note = note with { Bmp = null, BmpLength = null, BmpWritten = null };
        }

        files.WriteText(Path.ChangeExtension(written, ".json"), note.Write());
    }

    /// <summary>Sends the BMP to the Recycle Bin when it is still as it was read; false, with one log line, when it is not or cannot be.</summary>
    private bool Recycled(string source, string name, FileState read)
    {
        if (files.Stat(source) is not { } now || now != read)
        {
            logger.LogInformation("The screenshot {File} changed after it was read and was left in place", name);
            return false;
        }

        try
        {
            if (recycle(source))
            {
                return true;
            }

            logger.LogWarning("The screenshot {File} could not be moved to the Recycle Bin", name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The screenshot {File} could not be moved to the Recycle Bin", name);
        }

        return false;
    }

    /// <summary>Whether the BMP reached the length its header states within <see cref="Patience"/>.</summary>
    private async Task<bool> Arrived(string source)
    {
        for (var waited = TimeSpan.Zero; ; waited += Every)
        {
            if (Complete(source))
            {
                return true;
            }

            if (waited >= Patience)
            {
                return false;
            }

            await delay(Every);
        }
    }

    private bool Complete(string source)
    {
        if (files.Stat(source) is not { } state)
        {
            return false;
        }

        using var header = files.OpenRead(source);

        return header is not null && EliteScreenshot.ExpectedLength(header) is { } expected && state.Length >= expected;
    }

    /// <summary>A BMP stream to a JPEG at its own size.</summary>
    internal static byte[] ToJpeg(Stream bmp)
    {
        using var random = bmp.AsRandomAccessStream();

        var decoder = BitmapDecoder.CreateAsync(random).AsTask().GetAwaiter().GetResult();

        using var bitmap = decoder
            .GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return EliteWindowCapture.Encode(
            bitmap,
            BitmapEncoder.JpegEncoderId,
            EliteWindowCapture.JpegOptions(EliteWindowCapture.FullJpegQuality),
            (bitmap.PixelWidth, bitmap.PixelHeight));
    }
}
