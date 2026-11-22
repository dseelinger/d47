using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// A track with no read-ahead of its own keeps three seconds decoded ahead, as music always has; one that sets a
/// read-ahead, as a podcast episode does, keeps that much instead.
/// </summary>
public class ATrackKeepsItsOwnReadAheadTests
{
    private static readonly int ChunkBytes = AudioFormat.Music.SampleRate / 10 * AudioFormat.Music.BytesPerFrame;

    [Fact]
    public void MusicKeepsThreeSecondsAhead()
    {
        var pcm = new EndlessPcm();
        using var provider = new StreamSampleProvider(new MusicTrack("music", () => pcm), NullLogger.Instance);

        Assert.Equal(TimeSpan.FromSeconds(3), StreamSampleProvider.Ahead);
        WaitUntil(() => pcm.BytesRead >= Bytes(StreamSampleProvider.Ahead));
        Thread.Sleep(300);

        Assert.InRange(pcm.BytesRead, Bytes(StreamSampleProvider.Ahead), Bytes(StreamSampleProvider.Ahead) + ChunkBytes);
    }

    [Fact]
    public void ATrackWithItsOwnReadAheadKeepsThatMuch()
    {
        var ahead = TimeSpan.FromSeconds(30);
        var pcm = new EndlessPcm();
        using var provider = new StreamSampleProvider(
            new MusicTrack("episode", () => pcm, ReadAhead: ahead), NullLogger.Instance);

        WaitUntil(() => pcm.BytesRead >= Bytes(ahead));
        Thread.Sleep(300);

        Assert.InRange(pcm.BytesRead, Bytes(ahead), Bytes(ahead) + ChunkBytes);
    }

    private static long Bytes(TimeSpan time) =>
        (long)(time.TotalSeconds * AudioFormat.Music.SampleRate) * AudioFormat.Music.BytesPerFrame;

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "the condition never held");
            Thread.Sleep(5);
        }
    }

    /// <summary>Silence with no end, counting the bytes read.</summary>
    private sealed class EndlessPcm : IPcmStream
    {
        private long _bytesRead;

        public long BytesRead => Interlocked.Read(ref _bytesRead);

        public TimeSpan? Length => null;

        public int Read(Span<byte> buffer)
        {
            buffer.Clear();
            Interlocked.Add(ref _bytesRead, buffer.Length);
            return buffer.Length;
        }

        public void Seek(TimeSpan to)
        {
        }

        public void Dispose()
        {
        }
    }
}
