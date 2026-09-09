using D47.Core.Audio;
using NAudio.Wave;
using Xunit;

namespace D47.Audio.Tests;

public class ClipSampleProviderTests
{
    /// <summary>Little-endian 16-bit PCM, as everything in d47 carries it.</summary>
    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            bytes[i * 2] = (byte)samples[i];
            bytes[(i * 2) + 1] = (byte)(samples[i] >> 8);
        }

        return bytes;
    }

    private static AudioClip Clip(params short[] samples) =>
        new("test", Pcm(samples), AudioFormat.Standard);

    private static ClipSampleProvider Reader(bool loop, params short[] samples) =>
        new(Clip(samples), loop);

    [Fact]
    public void TheClipsFormatIsWhatTheMixerIsTold()
    {
        var provider = new ClipSampleProvider(
            new AudioClip("test", Pcm(0), new AudioFormat(24_000, 2)), loop: false);

        Assert.Equal(24_000, provider.WaveFormat.SampleRate);
        Assert.Equal(2, provider.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.IeeeFloat, provider.WaveFormat.Encoding);
    }

    [Fact]
    public void SamplesWidenFromLittleEndianShorts()
    {
        var buffer = new float[4];
        var read = Reader(loop: false, 0, 16384, -16384, short.MinValue).Read(buffer, 0, 4);

        Assert.Equal(4, read);
        Assert.Equal(0f, buffer[0]);
        Assert.Equal(0.5f, buffer[1], 5);
        Assert.Equal(-0.5f, buffer[2], 5);
        Assert.Equal(-1f, buffer[3], 5);
    }

    [Fact]
    public void ReadingHonoursTheOffset()
    {
        var buffer = new float[4];
        buffer[0] = 9f;

        var read = Reader(loop: false, 16384).Read(buffer, 1, 3);

        Assert.Equal(1, read);
        Assert.Equal(9f, buffer[0]);
        Assert.Equal(0.5f, buffer[1], 5);
    }

    /// <summary>A short read is how NAudio's mixer learns the input has ended.</summary>
    [Fact]
    public void AFinishedClipReportsAShortRead()
    {
        var provider = Reader(loop: false, 1000, 2000);
        var buffer = new float[8];

        Assert.Equal(2, provider.Read(buffer, 0, 8));
        Assert.Equal(0, provider.Read(buffer, 0, 8));
    }

    [Fact]
    public void ReadingTwiceContinuesRatherThanRestarting()
    {
        var provider = Reader(loop: false, 16384, -16384);
        var buffer = new float[1];

        provider.Read(buffer, 0, 1);
        Assert.Equal(0.5f, buffer[0], 5);

        provider.Read(buffer, 0, 1);
        Assert.Equal(-0.5f, buffer[0], 5);
    }

    [Fact]
    public void ALoopingClipWrapsAndFillsTheRequest()
    {
        var buffer = new float[5];
        var read = Reader(loop: true, 16384, -16384).Read(buffer, 0, 5);

        Assert.Equal(5, read);
        Assert.Equal([0.5f, -0.5f, 0.5f, -0.5f, 0.5f], [.. buffer.Select(sample => MathF.Round(sample, 4))]);
    }

    [Fact]
    public void AnEmptyLoopingClipEndsRatherThanSpinning()
    {
        var provider = Reader(loop: true);

        Assert.Equal(0, provider.Read(new float[16], 0, 16));
    }

    [Fact]
    public void AnEmptyClipThatDoesNotLoopEndsToo()
    {
        Assert.Equal(0, Reader(loop: false).Read(new float[16], 0, 16));
    }
}

public class RenderTapTests
{
    private static readonly WaveFormat Stereo = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    private static readonly WaveFormat Mono = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 1);

    private static short ShortAt(ReadOnlyMemory<byte> pcm, int index)
    {
        var span = pcm.Span;
        return (short)(span[index * 2] | (span[(index * 2) + 1] << 8));
    }

    [Fact]
    public void EverySampleReadIsReportedAsLittleEndianPcm()
    {
        var tap = new RenderTap(new ArraySampleProvider(Mono, 0f, 0.5f, -0.5f));
        RenderReferenceFrame? seen = null;
        tap.Rendered += frame => seen = frame;

        tap.Read(new float[3], 0, 3);

        Assert.NotNull(seen);
        Assert.Equal(6, seen.Value.Pcm.Length);
        Assert.Equal(0, ShortAt(seen.Value.Pcm, 0));
        Assert.Equal(16383, ShortAt(seen.Value.Pcm, 1));
        Assert.Equal(-16383, ShortAt(seen.Value.Pcm, 2));
    }

    [Fact]
    public void SamplesBeyondFullScaleAreClampedNotWrapped()
    {
        var tap = new RenderTap(new ArraySampleProvider(Mono, 2f, -2f));
        RenderReferenceFrame? seen = null;
        tap.Rendered += frame => seen = frame;

        tap.Read(new float[2], 0, 2);

        Assert.Equal(short.MaxValue, ShortAt(seen!.Value.Pcm, 0));
        Assert.Equal(short.MinValue, ShortAt(seen.Value.Pcm, 1));
    }

    /// <summary>A stereo buffer of eight floats advances the position by four.</summary>
    [Fact]
    public void ThePositionCountsFramesRatherThanSamples()
    {
        var tap = new RenderTap(new ArraySampleProvider(Stereo, new float[8]));
        var positions = new List<long>();
        tap.Rendered += frame => positions.Add(frame.SamplePosition);

        tap.Read(new float[4], 0, 4);
        tap.Read(new float[4], 0, 4);

        Assert.Equal([0, 2], positions);
    }

    [Fact]
    public void ThePositionAdvancesWithNoSubscriber()
    {
        var tap = new RenderTap(new ArraySampleProvider(Mono, new float[6]));

        tap.Read(new float[4], 0, 4);

        RenderReferenceFrame? seen = null;
        tap.Rendered += frame => seen = frame;

        tap.Read(new float[2], 0, 2);

        Assert.Equal(4, seen!.Value.SamplePosition);
    }

    [Fact]
    public void AnEmptyReadRaisesNothing()
    {
        var tap = new RenderTap(new ArraySampleProvider(Mono));
        var raised = 0;
        tap.Rendered += _ => raised++;

        Assert.Equal(0, tap.Read(new float[4], 0, 4));
        Assert.Equal(0, raised);
    }

    [Fact]
    public void TheFrameCarriesTheRenderFormat()
    {
        var tap = new RenderTap(new ArraySampleProvider(Stereo, new float[2]));
        RenderReferenceFrame? seen = null;
        tap.Rendered += frame => seen = frame;

        tap.Read(new float[2], 0, 2);

        Assert.Equal(48_000, seen!.Value.Format.SampleRate);
        Assert.Equal(2, seen.Value.Format.Channels);
    }

    [Fact]
    public void TheReadIsPassedThroughUnchanged()
    {
        var tap = new RenderTap(new ArraySampleProvider(Mono, 0.25f, 0.5f));
        var buffer = new float[4];

        Assert.Equal(2, tap.Read(buffer, 0, 4));
        Assert.Equal(0.25f, buffer[0]);
        Assert.Equal(0.5f, buffer[1]);
    }
}

/// <summary>NAudio identifies a mixer input by object reference and nothing else.</summary>
public class TrackedSampleProviderTests
{
    [Fact]
    public void TheIdAndTheFormatSurviveTheWrapping()
    {
        var source = new ArraySampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(24_000, 1), 0.5f);
        var tracked = new TrackedSampleProvider(42, source);

        Assert.Equal(42, tracked.Id);
        Assert.Same(source.WaveFormat, tracked.WaveFormat);
    }

    [Fact]
    public void ReadsGoStraightToTheSource()
    {
        var tracked = new TrackedSampleProvider(
            1, new ArraySampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(24_000, 1), 0.25f, 0.5f));

        var buffer = new float[2];

        Assert.Equal(2, tracked.Read(buffer, 0, 2));
        Assert.Equal([0.25f, 0.5f], buffer);
    }
}
