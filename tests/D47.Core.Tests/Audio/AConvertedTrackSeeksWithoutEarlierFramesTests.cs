using System.Buffers.Binary;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A resampled track seeks to the frame at the target, with nothing held from before it.</summary>
public class AConvertedTrackSeeksWithoutEarlierFramesTests
{
    private static readonly AudioFormat Source = new(44_100, 1);

    [Fact]
    public void TheLengthIsTheSourcesLength()
    {
        using var stream = Open(seconds: 2);

        Assert.Equal(TimeSpan.FromSeconds(2), stream.Length);
    }

    [Fact]
    public void TheSamplesAfterASeekAreTheSamplesAtThatOffsetReadStraightThrough()
    {
        byte[] straight;

        using (var whole = Open(seconds: 2))
        {
            straight = ReadAll(whole);
        }

        using var stream = Open(seconds: 2);
        stream.Read(new byte[48_000 * AudioFormat.Music.BytesPerFrame]);

        // 0.5 s is a whole frame at both rates: 22,050 in, 24,000 out.
        stream.Seek(TimeSpan.FromSeconds(0.5));
        var after = ReadAll(stream);

        var offset = 24_000 * AudioFormat.Music.BytesPerFrame;
        Assert.Equal(straight.Length - offset, after.Length);

        for (var at = 0; at < after.Length; at += 2)
        {
            var expected = BinaryPrimitives.ReadInt16LittleEndian(straight.AsSpan(offset + at));
            var actual = BinaryPrimitives.ReadInt16LittleEndian(after.AsSpan(at));

            // Read straight through, the interpolation position is a sum of 24,000 inexact steps; after the seek it
            // starts at zero. The difference can change a rounded sample by one, and by no more.
            Assert.True(Math.Abs(expected - actual) <= 1, $"sample {at / 2} after the seek is {actual}, not {expected}");
        }
    }

    private static IPcmStream Open(int seconds)
    {
        var frames = Source.SampleRate * seconds;
        var pcm = new byte[frames * Source.BytesPerFrame];

        for (var frame = 0; frame < frames; frame++)
        {
            var sample = Math.Sin(2 * Math.PI * 441 * frame / Source.SampleRate) * 16_000;
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(frame * 2), (short)sample);
        }

        return WavReader.OpenMusic(new MemoryStream(WavWriter.ToBytes(pcm, Source)), "test");
    }

    private static byte[] ReadAll(IPcmStream stream)
    {
        using var output = new MemoryStream();
        var chunk = new byte[3_000];
        int read;

        while ((read = stream.Read(chunk)) > 0)
        {
            output.Write(chunk, 0, read);
        }

        return output.ToArray();
    }
}
