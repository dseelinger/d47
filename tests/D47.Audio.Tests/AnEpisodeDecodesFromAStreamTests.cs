using D47.Core.Audio;
using NAudio.MediaFoundation;
using NAudio.Utils;
using NAudio.Wave;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// An MP3 held in a stream decodes to 48 kHz stereo 16-bit PCM and seeks, and a stream that cannot seek is refused
/// with a sentence, because Media Foundation cannot read one.
/// </summary>
[Trait("Category", "Integration")]
public class AnEpisodeDecodesFromAStreamTests
{
    private const int BytesPerSecond = 48_000 * 2 * 2;

    [Fact]
    public void AnMp3InAStreamReadsAsFortyEightKilohertzStereo()
    {
        var source = new TrackedStream(Mp3(seconds: 1), canSeek: true);
        var total = 0;
        var peak = 0;

        using (var stream = new MediaFoundationDecoder().Open(source))
        {
            Assert.True(stream.CanSeek);
            Assert.InRange(stream.Length!.Value.TotalMilliseconds, 990, 1100);

            var buffer = new byte[4_096];
            int read;

            while ((read = stream.Read(buffer)) > 0)
            {
                Assert.Equal(0, read % 4);

                for (var i = 0; i < read; i += 2)
                {
                    peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(buffer, i)));
                }

                total += read;
            }
        }

        // An MP3 carries encoder padding at each end.
        Assert.InRange(total, BytesPerSecond * 99 / 100, BytesPerSecond * 11 / 10);
        Assert.True(peak > 5_000, "the tone did not survive the decode");
        Assert.True(source.Disposed, "the stream was left open");
    }

    [Fact]
    public void ASeekInAStreamedEpisodeSkipsAhead()
    {
        using var stream = new MediaFoundationDecoder().Open(new TrackedStream(Mp3(seconds: 2), canSeek: true));

        stream.Seek(TimeSpan.FromSeconds(1.5));

        var buffer = new byte[4_096];
        var total = 0;
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            total += read;
        }

        Assert.InRange(total, BytesPerSecond * 40 / 100, BytesPerSecond * 65 / 100);
    }

    [Fact]
    public void AStreamThatCannotSeekIsRefusedWithASentence()
    {
        var source = new TrackedStream(Mp3(seconds: 1), canSeek: false);

        var error = Assert.Throws<AudioDecodeException>(() => new MediaFoundationDecoder().Open(source));

        Assert.Contains("only from the start", error.Message, StringComparison.Ordinal);
        Assert.True(source.Disposed, "the stream was left open");
    }

    private static byte[] Mp3(double seconds)
    {
        var format = new WaveFormat(44_100, 16, 2);
        var frames = (int)(format.SampleRate * seconds);
        var wav = new MemoryStream();

        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(wav), format))
        {
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = (float)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 0.5);
                writer.WriteSample(sample);
                writer.WriteSample(sample);
            }
        }

        wav.Position = 0;

        var mp3 = new MemoryStream();

        using (var reader = new WaveFileReader(wav))
        {
            MediaFoundationApi.Startup();
            MediaFoundationEncoder.EncodeToMp3(reader, mp3);
        }

        return mp3.ToArray();
    }

    /// <summary>A memory stream that can be made to refuse seeking, and says whether it was disposed.</summary>
    private sealed class TrackedStream(byte[] bytes, bool canSeek) : MemoryStream(bytes, writable: false)
    {
        public bool Disposed { get; private set; }

        public override bool CanSeek => canSeek && base.CanSeek;

        public override long Seek(long offset, SeekOrigin loc) =>
            canSeek ? base.Seek(offset, loc) : throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
