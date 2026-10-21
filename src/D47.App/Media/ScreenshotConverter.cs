using System.Collections.Concurrent;
using D47.App.Diagnostics;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;

namespace D47.App.Media;

/// <summary>
/// Copies each Elite screenshot to a named JPEG in a <c>D47</c> folder inside Elite's pictures folder, one at a time on
/// the pool. Elite's BMP is only read.
/// </summary>
public sealed class ScreenshotConverter(
    IFileSystem files,
    string folder,
    ILogger<ScreenshotConverter> logger,
    Func<Stream, byte[]> encode,
    Func<TimeSpan, Task> delay)
{
    /// <summary>How long a BMP has to reach its full length before it is skipped.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(120);

    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    private readonly ConcurrentQueue<(EliteScreenshot Shot, DateTimeOffset Taken)> _queue = new();

    private int _running;

    public ScreenshotConverter(IFileSystem files, string folder, ILogger<ScreenshotConverter> logger)
        : this(files, folder, logger, ToJpeg, delay => Task.Delay(delay))
    {
    }

    /// <summary>The folder the JPEGs are written to.</summary>
    public string Target => Path.Combine(folder, "D47");

    /// <summary>The pool's current run, for a test to wait on; completed when nothing is queued.</summary>
    internal Task Running { get; private set; } = Task.CompletedTask;

    /// <summary>On the tick: queues the tick's screenshots and starts the pool when it is idle. Reads no file.</summary>
    public void Observe(IReadOnlyList<JournalEvent> events, bool priming, bool enabled, JournalLocation fallback)
    {
        if (priming || !enabled)
        {
            return;
        }

        foreach (var journalEvent in events)
        {
            if (EliteScreenshot.TryParse(journalEvent, fallback) is { } shot)
            {
                _queue.Enqueue((shot, journalEvent.Timestamp));
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
                    await ConvertAsync(queued.Shot, queued.Taken);
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

    private async Task ConvertAsync(EliteScreenshot shot, DateTimeOffset taken)
    {
        var source = shot.SourcePath(folder);

        if (!await Arrived(source))
        {
            logger.LogInformation("The screenshot {File} did not arrive within {Seconds}s and was skipped", shot.FileName, Patience.TotalSeconds);
            return;
        }

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

        var local = taken.LocalDateTime;
        var copy = 1;

        while (files.Stat(Path.Combine(Target, shot.Name(local, copy))) is not null)
        {
            copy++;
        }

        files.WriteBytes(Path.Combine(Target, shot.Name(local, copy)), jpeg);
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
