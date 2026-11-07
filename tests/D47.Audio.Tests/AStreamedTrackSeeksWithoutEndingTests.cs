using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// A streamed track counts what has been copied out to the speakers, and a seek moves it without ending it: a seek
/// back while the last buffered second plays keeps the track going from the target rather than starting the next one.
/// </summary>
public class AStreamedTrackSeeksWithoutEndingTests
{
    private const int Rate = 48_000;

    /// <summary>Stereo float in the mix: eight bytes a frame.</summary>
    private const int MixBytesPerFrame = 8;

    [Fact]
    public void ProgressCountsTheFramesReadOutAndPublishesTheLength()
    {
        var pcm = new IndexedPcm(seconds: 2);
        using var provider = Provider(pcm);
        WaitUntil(() => pcm.ReachedEnd);

        Assert.Equal(new TrackProgress(TimeSpan.Zero, TimeSpan.FromSeconds(2)), provider.Progress);

        Read(provider, TimeSpan.FromSeconds(0.5));

        Assert.Equal(TimeSpan.FromSeconds(0.5), provider.Progress.Elapsed);
    }

    [Fact]
    public void TheSilenceOfAnUnderrunIsNotCounted()
    {
        using var release = new ManualResetEventSlim();
        using var provider = Provider(new IndexedPcm(seconds: 1, readGate: release));

        Assert.Equal(960, provider.Read(new float[960], 0, 960));
        Assert.Equal(TimeSpan.Zero, provider.Progress.Elapsed);

        release.Set();
    }

    [Fact]
    public void ElapsedIsClampedToALengthThatWasAnEstimate()
    {
        var pcm = new IndexedPcm(seconds: 2, reportedLength: TimeSpan.FromSeconds(1));
        using var provider = Provider(pcm);
        WaitUntil(() => pcm.ReachedEnd);

        Read(provider, TimeSpan.FromSeconds(1.5));

        Assert.Equal(new TrackProgress(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)), provider.Progress);
    }

    [Fact]
    public void ASeekBackInTheLastBufferedSecondKeepsPlayingFromTheTarget()
    {
        using var release = new ManualResetEventSlim();
        var pcm = new IndexedPcm(seconds: 2, seekGate: release);
        using var provider = Provider(pcm);
        WaitUntil(() => pcm.ReachedEnd);
        Read(provider, TimeSpan.FromSeconds(1.5));

        Assert.True(provider.Seek(TimeSpan.FromSeconds(0.5)));

        // The seek is held in the stream: rendering past the end neither waits on it nor ends the track.
        var samples = (int)(Rate * 1.0) * 2;
        Assert.Equal(samples, provider.Read(new float[samples], 0, samples));

        release.Set();
        WaitUntil(() => provider.Progress.Elapsed == TimeSpan.FromSeconds(0.5));
        WaitUntil(() => pcm.ReachedEnd);

        var rest = new float[(Rate * 2) * 2];
        Assert.Equal((int)(Rate * 1.5) * 2, provider.Read(rest, 0, rest.Length));
        Assert.Equal(IndexedPcm.SampleAt(Rate / 2) / 32768f, rest[0]);
        Assert.Equal(0, provider.Read(rest, 0, rest.Length));
    }

    [Fact]
    public void ASeekToTheLengthOrBeyondPlaysOutTheEnd()
    {
        var pcm = new IndexedPcm(seconds: 2);
        using var provider = Provider(pcm);
        WaitUntil(() => pcm.ReachedEnd);

        Assert.True(provider.Seek(TimeSpan.FromSeconds(30)));
        WaitUntil(() => provider.Progress.Elapsed == TimeSpan.FromSeconds(2));
        WaitUntil(() => provider.Read(new float[960], 0, 960) == 0);
    }

    [Fact]
    public void ATrackThatHasEndedOrFailedRefusesASeek()
    {
        using var ended = Provider(new IndexedPcm(seconds: 0.01));
        WaitUntil(() => ended.Read(new float[960], 0, 960) == 0);

        using var failed = new StreamSampleProvider(
            new MusicTrack("gone", () => throw new FileNotFoundException("gone")), NullLogger.Instance);
        WaitUntil(() => failed.Read(new float[960], 0, 960) == 0);

        Assert.False(ended.Seek(TimeSpan.Zero));
        Assert.False(failed.Seek(TimeSpan.Zero));
    }

    [Fact]
    public void TheSinkFinishesASeekedTrackOnceAtItsRealEnd()
    {
        using var release = new ManualResetEventSlim();
        var pcm = new IndexedPcm(seconds: 2, seekGate: release);
        var output = new RenderedByHand();
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, new FakeEndpointEnumerator(), _ => output);
        var finished = new List<long>();
        sink.Finished += finished.Add;
        sink.Open();

        sink.Play(PlaybackRequest.Stream(1, new MusicTrack("indexed", () => pcm), 1f));
        WaitUntil(() => pcm.ReachedEnd);
        WaitUntil(() => sink.Progress(1) is { Length: not null });
        Render(output, TimeSpan.FromSeconds(1.5));

        Assert.True(sink.Seek(1, TimeSpan.FromSeconds(0.5)));
        Render(output, TimeSpan.FromSeconds(1));
        Assert.Empty(finished);

        release.Set();
        WaitUntil(() => sink.Progress(1)?.Elapsed == TimeSpan.FromSeconds(0.5));
        WaitUntil(() =>
        {
            Render(output, TimeSpan.FromSeconds(0.1));
            return finished.Count > 0;
        });

        Assert.Equal([1L], finished);
        Assert.Null(sink.Progress(1));
        Assert.False(sink.Seek(1, TimeSpan.Zero));
    }

    [Fact]
    public void ASeekOnAPausedTrackMovesItAndLeavesItPaused()
    {
        var pcm = new IndexedPcm(seconds: 2);
        var output = new RenderedByHand();
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, new FakeEndpointEnumerator(), _ => output);
        sink.Open();
        sink.Play(PlaybackRequest.Stream(1, new MusicTrack("indexed", () => pcm), 1f));
        WaitUntil(() => pcm.ReachedEnd);
        sink.Pause(1);

        Assert.True(sink.Seek(1, TimeSpan.FromSeconds(1)));
        WaitUntil(() => sink.Progress(1)?.Elapsed == TimeSpan.FromSeconds(1));
        Render(output, TimeSpan.FromSeconds(0.5));

        Assert.Equal(TimeSpan.FromSeconds(1), sink.Progress(1)?.Elapsed);
    }

    [Fact]
    public void AClipOrAnIdNotPlayingHasNoProgressAndCannotSeek()
    {
        var output = new RenderedByHand();
        using var sink = new WasapiAudioSink(NullLogger<WasapiAudioSink>.Instance, new FakeEndpointEnumerator(), _ => output);
        sink.Open();
        sink.Play(new PlaybackRequest(1, new AudioClip("clip", new byte[48_000], new AudioFormat(Rate, 2)), Loop: false, Gain: 1f));

        Assert.Null(sink.Progress(1));
        Assert.False(sink.Seek(1, TimeSpan.Zero));
        Assert.Null(sink.Progress(2));
        Assert.False(sink.Seek(2, TimeSpan.Zero));
    }

    private static StreamSampleProvider Provider(IndexedPcm pcm) =>
        new(new MusicTrack("indexed", () => pcm), NullLogger.Instance);

    private static void Read(StreamSampleProvider provider, TimeSpan length)
    {
        var samples = (int)(Rate * length.TotalSeconds) * 2;
        provider.Read(new float[samples], 0, samples);
    }

    private static void Render(RenderedByHand output, TimeSpan length) =>
        output.Render((int)(Rate * length.TotalSeconds) * MixBytesPerFrame);

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "the condition never held");
            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// A stereo track whose samples say which frame they are, so a read after a seek shows where it landed. Reads
    /// and seeks can each be held on a gate.
    /// </summary>
    private sealed class IndexedPcm(
        double seconds,
        TimeSpan? reportedLength = null,
        ManualResetEventSlim? readGate = null,
        ManualResetEventSlim? seekGate = null) : IPcmStream
    {
        private readonly long _frames = (long)(Rate * seconds);
        private long _position;
        private volatile bool _reachedEnd;

        public bool ReachedEnd => _reachedEnd;

        public TimeSpan? Length { get; } = reportedLength ?? TimeSpan.FromSeconds(seconds);

        public static short SampleAt(long frame) => (short)(frame / 480);

        public int Read(Span<byte> buffer)
        {
            readGate?.Wait();

            var frames = (int)Math.Min(buffer.Length / 4, _frames - _position);

            for (var i = 0; i < frames; i++)
            {
                var sample = SampleAt(_position + i);

                for (var channel = 0; channel < 2; channel++)
                {
                    buffer[(i * 4) + (channel * 2)] = (byte)sample;
                    buffer[(i * 4) + (channel * 2) + 1] = (byte)(sample >> 8);
                }
            }

            _position += frames;

            if (frames == 0)
            {
                _reachedEnd = true;
            }

            return frames * 4;
        }

        public void Seek(TimeSpan to)
        {
            _reachedEnd = false;
            seekGate?.Wait();
            _position = Math.Min(_frames, to.Ticks * Rate / TimeSpan.TicksPerSecond);
        }

        public void Dispose()
        {
        }
    }
}
