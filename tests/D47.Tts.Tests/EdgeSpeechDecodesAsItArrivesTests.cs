using D47.Core.Audio;
using Xunit;

namespace D47.Tts.Tests;

/// <summary><see cref="Mp3StreamDecoder"/> against <see cref="EdgeNeuralTtsProvider.Decode"/> on a recorded Edge MP3.</summary>
[Trait("Category", "Integration")]
public class EdgeSpeechDecodesAsItArrivesTests
{
    private static readonly byte[] Recorded =
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "edge-sentence.mp3"));

    private static byte[] DecodeInPieces(IEnumerable<int> sizes)
    {
        using var decoder = new Mp3StreamDecoder(EdgeProtocol.SourceSampleRate, 1);
        using var pcm = new MemoryStream();
        var at = 0;

        foreach (var size in sizes)
        {
            var count = Math.Min(size, Recorded.Length - at);
            pcm.Write(decoder.Push(Recorded.AsSpan(at, count)));
            at += count;
        }

        pcm.Write(decoder.Push(Recorded.AsSpan(at)));
        return pcm.ToArray();
    }

    [Fact]
    public void TheRecordingIsNotEmptyOnceDecodedWhole()
    {
        Assert.True(EdgeNeuralTtsProvider.Decode(Recorded).Length > 24_000);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(417)]
    [InlineData(1000)]
    [InlineData(16_384)]
    public void BytesSplitAnywhereDecodeToTheSamePcmAsTheWholeFile(int size)
    {
        var expected = EdgeNeuralTtsProvider.Decode(Recorded);

        Assert.Equal(expected, DecodeInPieces(Enumerable.Repeat(size, Recorded.Length)));
    }

    [Fact]
    public void UnevenSplitsDecodeToTheSamePcmAsTheWholeFile()
    {
        var expected = EdgeNeuralTtsProvider.Decode(Recorded);

        Assert.Equal(expected, DecodeInPieces([3, 1, 2, 500, 13, 1, 4096, 2, 77, 1, 900]));
    }

    [Fact]
    public void AnotherSampleRateFailsWithTheMessageDecodeUses()
    {
        using var decoder = new Mp3StreamDecoder(48_000, 1);

        var whole = Assert.Throws<TtsException>(() => decoder.Push(Recorded));

        Assert.Equal("Edge Neural sent 24000 Hz / 1ch audio where 24 kHz mono was requested.", whole.Message);
    }

    [Fact]
    public void AnotherChannelCountFailsWithTheMessageDecodeUses()
    {
        using var decoder = new Mp3StreamDecoder(EdgeProtocol.SourceSampleRate, 2);

        var whole = Assert.Throws<TtsException>(() => decoder.Push(Recorded));

        Assert.Equal("Edge Neural sent 24000 Hz / 1ch audio where 24 kHz mono was requested.", whole.Message);
    }
}
