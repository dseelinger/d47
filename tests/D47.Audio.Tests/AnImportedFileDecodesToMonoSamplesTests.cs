using D47.Core.Audio;
using NAudio.Wave;
using Xunit;

namespace D47.Audio.Tests;

[Trait("Category", "Integration")]
public class AnImportedFileDecodesToMonoSamplesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-file-decoder-tests", Guid.NewGuid().ToString("n"));

    public AnImportedFileDecodesToMonoSamplesTests() => Directory.CreateDirectory(_root);

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
    public void AStereoWavComesOutMonoAtItsOwnRate()
    {
        var path = Path.Combine(_root, "tone.wav");

        using (var writer = new WaveFileWriter(path, new WaveFormat(44_100, 16, 2)))
        {
            for (var frame = 0; frame < 44_100; frame++)
            {
                var sample = (float)Math.Sin(2 * Math.PI * 440 * frame / 44_100) * 0.5f;
                writer.WriteSample(sample);
                writer.WriteSample(sample);
            }
        }

        var decoded = new MediaFoundationFileDecoder().Decode(path);

        Assert.Equal(44_100, decoded.SampleRate);
        Assert.InRange(decoded.Samples.Length, 44_000, 44_200);
        Assert.InRange(decoded.Samples.Max(), 0.45f, 0.55f);
    }

    [Fact]
    public void AFileOverTenMinutesIsRefusedBeforeItIsDecoded()
    {
        var path = Path.Combine(_root, "long.wav");

        using (var writer = new WaveFileWriter(path, new WaveFormat(8_000, 8, 1)))
        {
            writer.Write(new byte[8_000 * 601], 0, 8_000 * 601);
        }

        var error = Assert.Throws<AudioDecodeException>(() => new MediaFoundationFileDecoder().Decode(path));

        Assert.Contains("longer than 10 minutes", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAudioThrowsADecodeFailure()
    {
        var path = Path.Combine(_root, "song.mp3");
        File.WriteAllText(path, "this is not an mp3");

        Assert.Throws<AudioDecodeException>(() => new MediaFoundationFileDecoder().Decode(path));
    }
}
