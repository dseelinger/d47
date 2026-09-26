using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// A streamed track renders what its feeder read, fills an underrun with silence, and ends — which is
/// what raises Finished and starts the next track — when it is read to the end or fails.
/// </summary>
public class AStreamedTrackPlaysUntilItEndsOrFailsTests
{
    [Fact]
    public void ItRendersTheWholeTrackAndThenEnds()
    {
        using var provider = Provider(() => new FakePcm(frames: 4_800, sample: 16_384));
        WaitUntilDrained(provider);

        var buffer = new float[10_000];

        Assert.Equal(4_800, provider.Read(buffer, 0, buffer.Length));
        Assert.All(buffer[..4_800], sample => Assert.Equal(0.5f, sample));
        Assert.Equal(0, provider.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void ATrackThatWillNotOpenEndsAtOnce()
    {
        using var provider = Provider(() => throw new FileNotFoundException("gone"));
        WaitUntilDrained(provider);

        Assert.Equal(0, provider.Read(new float[100], 0, 100));
    }

    [Fact]
    public void ATrackThatFailsMidwayEndsAfterWhatItRead()
    {
        using var provider = Provider(() => new FakePcm(frames: 240, sample: 1, failAfter: true));
        WaitUntilDrained(provider);

        var buffer = new float[1_000];

        Assert.Equal(240, provider.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, provider.Read(buffer, 0, buffer.Length));
    }

    /// <summary>A read the feeder has not caught up with is silence, and the track is not over.</summary>
    [Fact]
    public void AnUnderrunIsSilenceRatherThanAnEnd()
    {
        using var release = new ManualResetEventSlim();
        using var provider = Provider(() => new FakePcm(frames: 0, sample: 0, gate: release));

        var buffer = Enumerable.Repeat(1f, 480).ToArray();

        Assert.Equal(480, provider.Read(buffer, 0, buffer.Length));
        Assert.All(buffer, sample => Assert.Equal(0f, sample));

        release.Set();
        WaitUntilDrained(provider);

        Assert.Equal(0, provider.Read(buffer, 0, buffer.Length));
    }

    /// <summary>Stopping a track releases its file; nothing keeps reading it in the background.</summary>
    [Fact]
    public void StoppingTheTrackClosesIt()
    {
        var pcm = new FakePcm(frames: int.MaxValue, sample: 1);
        var provider = Provider(() => pcm);

        provider.Dispose();
        WaitUntilDrained(provider);

        Assert.True(pcm.Disposed);

        // The buffer holds a few seconds at most, however long the track is.
        Assert.True(pcm.FramesRead <= (StreamSampleProvider.Ahead * 2).TotalSeconds * AudioFormat.Standard.SampleRate);
    }

    private static StreamSampleProvider Provider(Func<IPcmStream> open) =>
        new(new MusicTrack("test", open), NullLogger.Instance);

    private static void WaitUntilDrained(StreamSampleProvider provider)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!provider.Drained)
        {
            Assert.True(DateTime.UtcNow < deadline, "the feeder never finished");
            Thread.Sleep(5);
        }
    }

    /// <summary>A constant tone of a given length, optionally failing at its end or waiting to start.</summary>
    private sealed class FakePcm(int frames, short sample, bool failAfter = false, ManualResetEventSlim? gate = null)
        : IPcmStream
    {
        private long _left = frames;

        public bool Disposed { get; private set; }

        public long FramesRead { get; private set; }

        public int Read(Span<byte> buffer)
        {
            gate?.Wait();

            var count = (int)Math.Min(buffer.Length / 2, _left);

            if (count == 0 && failAfter)
            {
                throw new IOException("the disk went away");
            }

            for (var i = 0; i < count; i++)
            {
                buffer[i * 2] = (byte)sample;
                buffer[(i * 2) + 1] = (byte)(sample >> 8);
            }

            _left -= count;
            FramesRead += count;

            return count * 2;
        }

        public void Dispose() => Disposed = true;
    }
}
