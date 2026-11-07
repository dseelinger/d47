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
        var pcm = new FakePcm(frames: 4_800, sample: 16_384);
        using var provider = Provider(() => pcm);
        WaitUntil(() => pcm.ReachedEnd);

        var buffer = new float[10_000];

        Assert.Equal(4_800, provider.Read(buffer, 0, 4_800));
        Assert.All(buffer[..4_800], sample => Assert.Equal(0.5f, sample));
        WaitUntilEnded(provider);
    }

    [Fact]
    public void ATrackThatWillNotOpenEndsAtOnce()
    {
        using var provider = Provider(() => throw new FileNotFoundException("gone"));

        WaitUntilEnded(provider);
    }

    [Fact]
    public void ATrackThatFailsMidwayEndsAfterWhatItRead()
    {
        var pcm = new FakePcm(frames: 240, sample: 1, failAfter: true);
        using var provider = Provider(() => pcm);
        WaitUntil(() => pcm.ReachedEnd);

        Assert.Equal(240, provider.Read(new float[1_000], 0, 240));
        WaitUntilEnded(provider);
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
        WaitUntilEnded(provider);
    }

    /// <summary>Stopping a track releases its file; nothing keeps reading it in the background.</summary>
    [Fact]
    public void StoppingTheTrackClosesIt()
    {
        var pcm = new FakePcm(frames: int.MaxValue, sample: 1);
        var provider = Provider(() => pcm);

        provider.Dispose();
        WaitUntil(() => pcm.Disposed);

        Assert.True(pcm.Disposed);

        // The buffer holds a few seconds at most, however long the track is.
        Assert.True(pcm.FramesRead <= (StreamSampleProvider.Ahead * 2).TotalSeconds * AudioFormat.Standard.SampleRate);
    }

    private static StreamSampleProvider Provider(Func<IPcmStream> open) =>
        new(new MusicTrack("test", open), NullLogger.Instance);

    private static void WaitUntilEnded(StreamSampleProvider provider) =>
        WaitUntil(() => provider.Read(new float[100], 0, 100) == 0);

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "the condition never held");
            Thread.Sleep(5);
        }
    }

    /// <summary>A constant tone of a given length, optionally failing at its end or waiting to start.</summary>
    private sealed class FakePcm(int frames, short sample, bool failAfter = false, ManualResetEventSlim? gate = null)
        : IPcmStream
    {
        private long _left = frames;
        private volatile bool _disposed;
        private volatile bool _reachedEnd;

        public bool ReachedEnd => _reachedEnd;

        public bool Disposed => _disposed;

        public long FramesRead { get; private set; }

        public int Read(Span<byte> buffer)
        {
            gate?.Wait();

            var count = (int)Math.Min(buffer.Length / 2, _left);

            if (count == 0)
            {
                _reachedEnd = true;
            }

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

        public TimeSpan? Length => null;

        public void Seek(TimeSpan to)
        {
        }

        public void Dispose() => _disposed = true;
    }
}
