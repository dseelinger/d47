using D47.App.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

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

    [Fact]
    public async Task TwentyFilesCopiedInOneGoGiveOneRebuild()
    {
        var rebuilds = 0;
        using var watch = new AudioFolderWatch(
            _root, () => Interlocked.Increment(ref rebuilds), NullLogger.Instance);

        for (var i = 0; i < 20; i++)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(_root, "cues", "listening", $"clip{i}.wav"),
                new byte[64],
                TestContext.Current.CancellationToken);
        }

        await Task.Delay(AudioFolderWatch.QuietFor - TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref rebuilds));

        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref rebuilds));
    }

    [Fact]
    public async Task ARebuildThatThrowsDoesNotStopTheNextOne()
    {
        var attempts = 0;
        using var watch = new AudioFolderWatch(
            _root,
            () =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    throw new InvalidDataException("a bad file");
                }
            },
            NullLogger.Instance);

        await File.WriteAllBytesAsync(Path.Combine(_root, "cues", "one.wav"), new byte[64], TestContext.Current.CancellationToken);
        await Task.Delay(AudioFolderWatch.QuietFor + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref attempts));

        await File.WriteAllBytesAsync(Path.Combine(_root, "cues", "two.wav"), new byte[64], TestContext.Current.CancellationToken);
        await Task.Delay(AudioFolderWatch.QuietFor + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(2, Volatile.Read(ref attempts));
    }
}
