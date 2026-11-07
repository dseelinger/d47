using System.Buffers.Binary;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A WAV track reports its length and seeks to the frame at the target, whatever came before its data.</summary>
public class AWavTrackSeeksToWholeFramesTests
{
    private static readonly AudioFormat Format = new(8_000, 2);

    [Fact]
    public void TheLengthIsTheDataOverTheBytesPerSecond()
    {
        using var stream = Open(frames: 12_000);

        Assert.Equal(TimeSpan.FromSeconds(1.5), stream.Length);
    }

    [Fact]
    public void ASeekForwardReadsFromTheTarget()
    {
        using var stream = Open(frames: 8_000);

        stream.Seek(TimeSpan.FromSeconds(0.5));

        Assert.Equal(4_000, FirstFrame(stream));
    }

    [Fact]
    public void ASeekBackReadsFromTheTarget()
    {
        using var stream = Open(frames: 8_000);
        Read(stream, frames: 6_000);

        stream.Seek(TimeSpan.FromSeconds(0.25));

        Assert.Equal(2_000, FirstFrame(stream));
        Assert.Equal(5_999, Read(stream, frames: 8_000).Length / Format.BytesPerFrame);
    }

    [Fact]
    public void ASeekPastTheEndReadsNothing()
    {
        using var stream = Open(frames: 8_000);

        stream.Seek(TimeSpan.FromSeconds(3));

        Assert.Equal(0, stream.Read(new byte[64]));
    }

    [Fact]
    public void ASeekAfterTheEndWasReachedReadsAgain()
    {
        using var stream = Open(frames: 8_000);
        Read(stream, frames: 8_000);
        Assert.Equal(0, stream.Read(new byte[64]));

        stream.Seek(TimeSpan.FromSeconds(0.75));

        Assert.Equal(6_000, FirstFrame(stream));
    }

    [Fact]
    public void ASeekToZeroLandsOnTheFirstSamplePastAChunkBeforeTheData()
    {
        using var stream = Open(frames: 8_000, chunkBeforeData: true);
        Read(stream, frames: 100);

        stream.Seek(TimeSpan.Zero);

        Assert.Equal(0, FirstFrame(stream));
    }

    [Fact]
    public void ANegativeSeekLandsOnTheFirstSample()
    {
        using var stream = Open(frames: 8_000);
        Read(stream, frames: 100);

        stream.Seek(TimeSpan.FromSeconds(-1));

        Assert.Equal(0, FirstFrame(stream));
    }

    /// <summary>Each frame's left sample is its own index and its right the negative, so a read shows where it is.</summary>
    private static WavPcmStream Open(int frames, bool chunkBeforeData = false)
    {
        var pcm = new byte[frames * Format.BytesPerFrame];

        for (var frame = 0; frame < frames; frame++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(frame * 4), (short)frame);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan((frame * 4) + 2), (short)-frame);
        }

        var file = WavWriter.ToBytes(pcm, Format);

        if (chunkBeforeData)
        {
            // fmt ends at byte 36 of the canonical header; a LIST chunk of 6 bytes goes between it and data.
            byte[] list = [.. "LIST"u8, 6, 0, 0, 0, .. "INFOab"u8];
            file = [.. file.AsSpan(0, 36), .. list, .. file.AsSpan(36)];
        }

        return WavReader.Open(new MemoryStream(file), "test");
    }

    private static int FirstFrame(WavPcmStream stream)
    {
        var frame = Read(stream, frames: 1);
        var left = BinaryPrimitives.ReadInt16LittleEndian(frame);

        Assert.Equal(-left, BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(2)));
        return left;
    }

    private static byte[] Read(WavPcmStream stream, int frames)
    {
        var buffer = new byte[frames * Format.BytesPerFrame];
        var total = 0;
        int read;

        while (total < buffer.Length && (read = stream.Read(buffer.AsSpan(total))) > 0)
        {
            total += read;
        }

        return buffer[..total];
    }
}
