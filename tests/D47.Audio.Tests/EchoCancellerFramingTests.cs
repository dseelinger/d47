using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

public class EchoCancellerFramingTests
{
    private const int CaptureRate = 48_000;

    /// <summary>Ten milliseconds at the capture rate.</summary>
    private const int Frame = CaptureRate / 100;

    private static EchoCanceller Started(RecordingCaptureSink next, FakeRenderTap tap)
    {
        var canceller = new EchoCanceller(next, tap, CaptureRate, NullLogger<EchoCanceller>.Instance);

        canceller.Start();

        Assert.SkipUnless(
            canceller.IsActive,
            $"the native WebRTC APM did not load here: {canceller.Unavailable}");

        return canceller;
    }

    [Fact]
    public void APartialFrameIsHeldBack()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame - 1]);

        Assert.Empty(next.Writes);
    }

    [Fact]
    public void AFullFrameIsForwarded()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame]);

        Assert.Single(next.Writes);
        Assert.Equal(Frame, next.Writes[0].Length);
    }

    [Fact]
    public void TwoShortWritesMakeOneFrame()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame - 100]);
        Assert.Empty(next.Writes);

        canceller.Write(new float[100]);

        Assert.Single(next.Writes);
        Assert.Equal(Frame, next.Writes[0].Length);
    }

    [Fact]
    public void ALongWriteBecomesWholeFramesWithTheTailHeld()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[(Frame * 3) + 37]);

        Assert.Equal(3, next.Writes.Count);
        Assert.All(next.Writes, write => Assert.Equal(Frame, write.Length));

        // And the 37 held samples arrive once the frame they belong to is complete.
        canceller.Write(new float[Frame - 37]);
        Assert.Equal(4, next.Writes.Count);
    }

    [Fact]
    public void ResetDropsThePartialFrame()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame - 10]);
        canceller.Reset();

        // Ten more would have completed the frame had the remainder survived.
        canceller.Write(new float[10]);

        Assert.Empty(next.Writes);
    }

    [Fact]
    public void AReferenceFrameIsAcceptedWhileRunning()
    {
        var next = new RecordingCaptureSink();
        var tap = new FakeRenderTap();
        using var canceller = Started(next, tap);

        Assert.True(tap.Subscribed);

        // 480 stereo frames of silence: 480 * 2 channels * 2 bytes.
        tap.Raise(new RenderReferenceFrame(0, new byte[480 * 2 * 2], new AudioFormat(48_000, 2)));

        canceller.Write(new float[Frame]);

        Assert.Single(next.Writes);
    }

    [Fact]
    public void AChangeOfReferenceFormatIsFollowed()
    {
        var next = new RecordingCaptureSink();
        var tap = new FakeRenderTap();
        using var canceller = Started(next, tap);

        tap.Raise(new RenderReferenceFrame(0, new byte[480 * 2 * 2], new AudioFormat(48_000, 2)));
        tap.Raise(new RenderReferenceFrame(480, new byte[441 * 2], new AudioFormat(44_100, 1)));

        canceller.Write(new float[Frame]);

        Assert.Single(next.Writes);
        Assert.True(canceller.IsActive);
    }

    [Fact]
    public void SilencePassesThroughTheModuleAsSilence()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame]);

        Assert.Single(next.Writes);
        Assert.All(next.Writes[0], sample => Assert.Equal(0f, sample));
    }

    /// <summary>Twenty frames of a 440 Hz tone at half scale, and the loudest sample that survives.</summary>
    private static float LoudestToneSampleThrough(EchoCanceller canceller, RecordingCaptureSink next)
    {
        var tone = new float[Frame];
        var phase = 0;

        // The APM ramps up: the first frames after init report convergence, not configuration.
        for (var pass = 0; pass < 20; pass++)
        {
            for (var i = 0; i < tone.Length; i++, phase++)
            {
                tone[i] = 0.5f * MathF.Sin(2f * MathF.PI * 440f * phase / CaptureRate);
            }

            canceller.Write(tone);
        }

        Assert.Equal(20, next.Writes.Count);

        return next.Writes[^1].Max(MathF.Abs);
    }

    [Fact]
    public void NoiseSuppressionAttenuatesASteadyToneAndTheRowIsWhatDecides()
    {
        var suppressedSink = new RecordingCaptureSink();
        using var suppressed = Started(suppressedSink, new FakeRenderTap());

        Assert.True(suppressed.SuppressNoise, "the shipped default is on, which is what this describes");

        var withSuppression = LoudestToneSampleThrough(suppressed, suppressedSink);

        var plainSink = new RecordingCaptureSink();
        using var plain = new EchoCanceller(
            plainSink, new FakeRenderTap(), CaptureRate, NullLogger<EchoCanceller>.Instance)
        {
            SuppressNoise = false,
        };

        plain.Start();
        Assert.SkipUnless(plain.IsActive, $"the native WebRTC APM did not load here: {plain.Unavailable}");

        var withoutSuppression = LoudestToneSampleThrough(plain, plainSink);

        // Both gain controllers are off, so the tone is present but not amplified.
        Assert.InRange(withoutSuppression, 0.05f, 0.75f);

        Assert.True(
            withSuppression < withoutSuppression * 0.5f,
            $"suppression on measured {withSuppression:0.0000}, off measured {withoutSuppression:0.0000}");
    }
}
