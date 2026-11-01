using NAudio.Wave;
using Xunit;

namespace D47.Audio.Tests;

public class AFadeRampsPerSampleTests
{
    private const int Rate = 48_000;

    private sealed class CountingSource : ISampleProvider
    {
        public int SamplesRead { get; private set; }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Fill(buffer, 1f, offset, count);
            SamplesRead += count;
            return count;
        }
    }

    private static float[] ReadFrames(FadeSampleProvider fade, int frames)
    {
        var buffer = new float[frames * 2];
        Assert.Equal(buffer.Length, fade.Read(buffer, 0, buffer.Length));
        return buffer;
    }

    [Fact]
    public void ARampReachesItsTargetInTheStatedNumberOfSamples()
    {
        var fade = new FadeSampleProvider(new CountingSource());
        fade.Fade(0f, TimeSpan.FromSeconds(1), holdAtEnd: false);

        var first = ReadFrames(fade, Rate - 1);
        Assert.True(first[^2] >= 0f);
        Assert.True(first[^2] < 0.001f);

        var last = ReadFrames(fade, 1);
        Assert.Equal(0f, last[0]);
        Assert.Equal(0f, fade.Level);
        Assert.Equal(1f, ReadFrames(new FadeSampleProvider(new CountingSource()), 1)[0]);
    }

    [Fact]
    public void TheRampIsAppliedWithinOneBuffer()
    {
        var fade = new FadeSampleProvider(new CountingSource());
        fade.Fade(0f, TimeSpan.FromSeconds(1), holdAtEnd: false);

        var buffer = ReadFrames(fade, Rate / 2);

        Assert.Equal(0.5f, buffer[^2], 2);
        Assert.True(buffer[0] > buffer[^2]);
    }

    [Fact]
    public void ASetGainDuringAFadeLeavesTheRampIntact()
    {
        var source = new CountingSource();
        var volume = new NAudio.Wave.SampleProviders.VolumeSampleProvider(source) { Volume = 1f };
        var fade = new FadeSampleProvider(volume);
        fade.Fade(0f, TimeSpan.FromSeconds(1), holdAtEnd: false);

        ReadFrames(fade, Rate / 2);
        volume.Volume = 0.5f;
        var buffer = ReadFrames(fade, Rate / 2);

        Assert.Equal(0f, buffer[^2], 3);
        Assert.Equal(0.25f * (1f - (((Rate / 2) + 1f) / Rate)) * 2f, buffer[0], 3);
    }

    [Fact]
    public void AHoldAtTheEndRendersSilenceWithoutReadingTheSource()
    {
        var source = new CountingSource();
        var fade = new FadeSampleProvider(source);
        fade.Fade(0f, TimeSpan.FromMilliseconds(100), holdAtEnd: true);

        var ramp = ReadFrames(fade, Rate / 10);
        var readAtEnd = source.SamplesRead;
        var held = ReadFrames(fade, 1000);

        Assert.Equal(0f, ramp[^1]);
        Assert.Equal(Rate / 10 * 2, readAtEnd);
        Assert.All(held, sample => Assert.Equal(0f, sample));
        Assert.Equal(readAtEnd, source.SamplesRead);
    }

    [Fact]
    public void AHoldEndsTheReadAtTheEndOfTheRampMidBuffer()
    {
        var source = new CountingSource();
        var fade = new FadeSampleProvider(source);
        fade.Fade(0.5f, TimeSpan.FromMilliseconds(10), holdAtEnd: true);

        var buffer = ReadFrames(fade, Rate / 10);

        Assert.Equal(Rate / 100 * 2, source.SamplesRead);
        Assert.Equal(0.5f, buffer[(Rate / 100 * 2) - 1], 5);
        Assert.Equal(0f, buffer[Rate / 100 * 2]);
    }

    [Fact]
    public void ReleasingAHoldContinuesAtTheLevelItWasLeftAt()
    {
        var source = new CountingSource();
        var fade = new FadeSampleProvider(source);
        fade.Fade(0.5f, TimeSpan.FromMilliseconds(10), holdAtEnd: true);
        ReadFrames(fade, Rate / 10);

        fade.Release();
        var buffer = ReadFrames(fade, 100);

        Assert.All(buffer, sample => Assert.Equal(0.5f, sample));
    }
}
