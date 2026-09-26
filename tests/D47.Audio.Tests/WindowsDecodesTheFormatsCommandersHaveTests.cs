using D47.Core.Audio;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>Media Foundation reads MP3 and non-standard WAV into 48 kHz mono 16-bit.</summary>
public class WindowsDecodesTheFormatsCommandersHaveTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-decoder-tests",
        Guid.NewGuid().ToString("n"));

    public WindowsDecodesTheFormatsCommandersHaveTests() => Directory.CreateDirectory(_root);

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
    public void AStereoFortyFourKilohertzWavComesOutStandard()
    {
        var path = WriteTone("tone.wav", seconds: 0.5);

        var clip = new MediaFoundationDecoder().Decode(path, "tone");

        Assert.Equal("tone", clip.Name);
        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.InRange(clip.Duration.TotalMilliseconds, 490, 510);
        Assert.True(Peak(clip.Pcm.Span) > 5000, "the tone did not survive the downmix");
    }

    [Fact]
    public void AnMp3ComesOutStandard()
    {
        var wav = WriteTone("tone.wav", seconds: 1);
        var mp3 = Path.Combine(_root, "tone.mp3");

        using (var reader = new WaveFileReader(wav))
        {
            MediaFoundationApi.Startup();
            MediaFoundationEncoder.EncodeToMp3(reader, mp3);
        }

        var clip = new MediaFoundationDecoder().Decode(mp3, "tone");

        Assert.Equal(AudioFormat.Standard, clip.Format);

        // An MP3 carries encoder padding at each end.
        Assert.InRange(clip.Duration.TotalMilliseconds, 990, 1100);
        Assert.True(Peak(clip.Pcm.Span) > 5000, "the tone did not survive the decode");
    }

    [Fact]
    public void AStreamedFileReadsToTheSameLengthAsADecodedOne()
    {
        var path = WriteTone("tone.wav", seconds: 0.25);
        var decoder = new MediaFoundationDecoder();

        var whole = decoder.Decode(path, "tone").Pcm.Length;

        using var stream = decoder.Open(path);
        var buffer = new byte[1001];
        var total = 0;
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            Assert.Equal(0, read % 2);
            total += read;
        }

        Assert.Equal(whole, total);
    }

    /// <summary>A file Windows cannot read is a reason to report, not an unhandled COM error.</summary>
    [Fact]
    public void AFileThatIsNotAudioThrowsADecodeFailure()
    {
        var path = Path.Combine(_root, "song.mp3");
        File.WriteAllText(path, "this is not an mp3");

        var error = Assert.Throws<AudioDecodeException>(() => new MediaFoundationDecoder().Decode(path, "song"));

        Assert.StartsWith("Windows", error.Message, StringComparison.Ordinal);
    }

    private string WriteTone(string name, double seconds)
    {
        var path = Path.Combine(_root, name);
        var format = new WaveFormat(44_100, 16, 2);
        var frames = (int)(format.SampleRate * seconds);

        using var writer = new WaveFileWriter(path, format);

        for (var frame = 0; frame < frames; frame++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 16_000);
            writer.WriteSample(sample / 32768f);
            writer.WriteSample(sample / 32768f);
        }

        return path;
    }

    private static int Peak(ReadOnlySpan<byte> pcm)
    {
        var peak = 0;

        for (var offset = 0; offset + 1 < pcm.Length; offset += 2)
        {
            peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm[offset..])));
        }

        return peak;
    }
}
