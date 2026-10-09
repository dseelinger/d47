using D47.App.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public sealed class DroppedInAudioIsRebuiltOffTheTickTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-audio-watch-tests",
        Guid.NewGuid().ToString("n"));

    public DroppedInAudioIsRebuiltOffTheTickTests() =>
        Directory.CreateDirectory(Path.Combine(_root, "cues", "listening"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }

    private AudioFolderWatch Watch(ManualClock clock, Action rebuild) =>
        new(_root, rebuild, NullLogger.Instance, clock.Starter, () => clock.Now);

    private static async Task WriteAsync(string path)
    {
        await File.WriteAllBytesAsync(path, new byte[64], TestContext.Current.CancellationToken);
    }

    /// <summary>Waits until the watcher has delivered at least <paramref name="count"/> events and gone quiet.</summary>
    private static void Delivered(AudioFolderWatch watch, int count)
    {
        var deadline = Environment.TickCount64 + 5000;
        var last = -1;

        while (Environment.TickCount64 < deadline)
        {
            var seen = watch.ChangesSeen;

            if (seen >= count && seen == last)
            {
                return;
            }

            last = seen;
            Thread.Sleep(50);
        }

        Assert.Fail("The folder watch never reported the files.");
    }

    [Fact]
    public async Task TwentyFilesCopiedInOneGoGiveOneRebuild()
    {
        var clock = new ManualClock();
        var rebuilds = 0;
        using var watch = Watch(clock, () => Interlocked.Increment(ref rebuilds));

        for (var i = 0; i < 20; i++)
        {
            await WriteAsync(Path.Combine(_root, "cues", "listening", $"clip{i}.wav"));
        }

        Delivered(watch, 20);

        clock.Advance(AudioFolderWatch.QuietFor - TimeSpan.FromSeconds(1));
        Assert.Equal(0, Volatile.Read(ref rebuilds));

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, Volatile.Read(ref rebuilds));
    }

    [Fact]
    public async Task ARebuildThatThrowsDoesNotStopTheNextOne()
    {
        var clock = new ManualClock();
        var attempts = 0;
        using var watch = Watch(
            clock,
            () =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    throw new InvalidDataException("a bad file");
                }
            });

        await WriteAsync(Path.Combine(_root, "cues", "one.wav"));
        Delivered(watch, 1);
        clock.Advance(AudioFolderWatch.QuietFor + TimeSpan.FromSeconds(1));
        Assert.Equal(1, Volatile.Read(ref attempts));

        var seen = watch.ChangesSeen;
        await WriteAsync(Path.Combine(_root, "cues", "two.wav"));
        Delivered(watch, seen + 1);
        clock.Advance(AudioFolderWatch.QuietFor + TimeSpan.FromSeconds(1));
        Assert.Equal(2, Volatile.Read(ref attempts));
    }
}
