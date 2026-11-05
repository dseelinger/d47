using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.MediaFoundation;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>Windows' encoder compresses a spoken clip in memory, and the bytes read back to a clip the speech channel plays.</summary>
[Trait("Category", "Integration")]
public sealed class ASpokenClipIsKeptCompressedTests
{
    private static readonly MediaFoundationClipCompressor Compressor = new(NullLogger<MediaFoundationClipCompressor>.Instance);

    [Fact]
    public void AFifteenSecondClipCompressesToUnderTwoHundredKilobytes()
    {
        var compressed = Compressor.Compress(Tone(AudioFormat.Standard, seconds: 15));

        Assert.NotNull(compressed);
        Assert.Equal(".m4a", compressed.Extension);
        Assert.True(compressed.Bytes.Length < 200 * 1024, $"{compressed.Bytes.Length} bytes");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(1)]
    public void TheCompressedBytesReadBackToTheSameLengthInTheStandardFormat(double seconds)
    {
        var clip = Tone(AudioFormat.Standard, seconds);

        var decoded = Compressor.Decompress(Compressor.Compress(clip)!.Bytes, "line");

        Assert.NotNull(decoded);
        Assert.Equal("line", decoded.Name);
        Assert.Equal(AudioFormat.Standard, decoded.Format);
        Assert.InRange(decoded.Duration.TotalMilliseconds, (seconds * 1000) - 100, (seconds * 1000) + 100);
        Assert.True(Peak(decoded.Pcm.Span) > 5000, "the tone did not survive the round trip");
    }

    [Fact]
    public void AClipInAnotherFormatIsConvertedBeforeItIsEncoded()
    {
        var clip = Tone(new AudioFormat(22_050, 2), seconds: 2);

        var decoded = Compressor.Decompress(Compressor.Compress(clip)!.Bytes, "line");

        Assert.NotNull(decoded);
        Assert.Equal(AudioFormat.Standard, decoded.Format);
        Assert.InRange(decoded.Duration.TotalMilliseconds, 1900, 2100);
    }

    [Fact]
    public void AMissingEncoderIsReportedAsNoClipRatherThanThrown()
    {
        var missing = new MediaFoundationClipCompressor(
            Guid.NewGuid(),
            TranscodeContainerTypes.MFTranscodeContainerType_MPEG4,
            ".m4a",
            NullLogger<MediaFoundationClipCompressor>.Instance);

        Assert.Null(missing.Compress(Tone(AudioFormat.Standard, seconds: 1)));
    }

    [Fact]
    public void AnEmptyClipIsNotCompressed() =>
        Assert.Null(Compressor.Compress(new AudioClip("line", Array.Empty<byte>(), AudioFormat.Standard)));

    [Fact]
    public void BytesThatAreNotAudioDecodeToNoClipRatherThanThrowing() =>
        Assert.Null(Compressor.Decompress("this is not audio"u8.ToArray(), "line"));

    private static AudioClip Tone(AudioFormat format, double seconds)
    {
        var frames = (int)(format.SampleRate * seconds);
        var pcm = new byte[frames * format.BytesPerFrame];

        for (var frame = 0; frame < frames; frame++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 16_000);

            for (var channel = 0; channel < format.Channels; channel++)
            {
                BitConverter.TryWriteBytes(pcm.AsSpan((frame * format.BytesPerFrame) + (channel * 2)), sample);
            }
        }

        return new AudioClip("tone", pcm, format);
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
