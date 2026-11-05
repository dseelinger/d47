using System.Buffers.Binary;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class MusicIsStreamedInStereoTests
{
    [Fact]
    public void ALeftOnlyToneStaysOnTheLeft()
    {
        var samples = Stream(Frames(2, 100, (_, channel) => channel == 0 ? 1000 : 0), 48_000, 2);

        Assert.Equal(200, samples.Length);
        Assert.All(samples.Where((_, i) => i % 2 == 0), s => Assert.Equal(1000, s));
        Assert.All(samples.Where((_, i) => i % 2 == 1), s => Assert.Equal(0, s));
    }

    [Fact]
    public void AMonoTrackPlaysTheSameOnBothChannels()
    {
        var samples = Stream(Frames(1, 50, (frame, _) => frame * 10), 48_000, 1);

        Assert.Equal(100, samples.Length);

        for (var frame = 0; frame < 50; frame++)
        {
            Assert.Equal(frame * 10, samples[frame * 2]);
            Assert.Equal(frame * 10, samples[(frame * 2) + 1]);
        }
    }

    [Fact]
    public void MoreThanTwoChannelsAreFoldedToStereo()
    {
        // Channels 0 and 2 go left, 1 and 3 go right.
        var samples = Stream(Frames(4, 10, (_, channel) => channel switch { 0 => 100, 1 => 300, 2 => 300, _ => 500 }), 48_000, 4);

        Assert.Equal(20, samples.Length);
        Assert.All(samples.Where((_, i) => i % 2 == 0), s => Assert.Equal(200, s));
        Assert.All(samples.Where((_, i) => i % 2 == 1), s => Assert.Equal(400, s));
    }

    [Fact]
    public void ARateOtherThan48KilohertzIsResampledAndKeepsItsChannelsApart()
    {
        var samples = Stream(Frames(2, 441, (_, channel) => channel == 0 ? 800 : -800), 24_000, 2);

        Assert.InRange(samples.Length / 2, 880, 882);
        Assert.All(samples.Where((_, i) => i % 2 == 0), s => Assert.Equal(800, s));
        Assert.All(samples.Where((_, i) => i % 2 == 1), s => Assert.Equal(-800, s));
    }

    [Fact]
    public void AStereoWavIsAlreadyMusicAndPassesThroughUntouched()
    {
        var bytes = WavWriter.ToBytes(Frames(2, 10, (_, _) => 1), AudioFormat.Music);
        using var stream = WavReader.OpenMusic(new MemoryStream(bytes), "same");

        Assert.IsType<WavPcmStream>(stream);
    }

    private static short[] Stream(byte[] pcm, int rate, int channels)
    {
        var bytes = WavWriter.ToBytes(pcm, new AudioFormat(rate, channels));
        using var stream = WavReader.OpenMusic(new MemoryStream(bytes), "track");
        using var output = new MemoryStream();
        var buffer = new byte[64];
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, read);
        }

        var all = output.ToArray();
        var samples = new short[all.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(all.AsSpan(i * 2));
        }

        return samples;
    }

    private static byte[] Frames(int channels, int frames, Func<int, int, int> sample)
    {
        var bytes = new byte[frames * channels * 2];

        for (var i = 0; i < frames * channels; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)sample(i / channels, i % channels));
        }

        return bytes;
    }
}
