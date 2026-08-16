using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// The framing, which is the whole of what <see cref="EchoCanceller"/> adds to WebRTC's AEC3
/// (list.md Phase 13).
/// <para>
/// The module takes exactly 10 ms at a time on both streams and WASAPI hands over whatever it
/// feels like on either, so both directions are accumulated into 10 ms frames and the remainder
/// is carried. Getting that wrong does not throw — it feeds the filter torn frames, and a filter
/// fed torn frames stops cancelling while still reporting itself active.
/// </para>
/// <para>
/// <b>These need the native module, which is a runtime dependency rather than hardware</b> — no
/// microphone, no speaker, no device of any kind. They skip rather than fail where it will not
/// load, because a machine missing the Visual C++ runtime is a machine where the shipped
/// behaviour is push-to-talk with no cancellation, and that is covered by
/// <see cref="EchoCancellerTests"/> instead.
/// </para>
/// </summary>
public class EchoCancellerFramingTests
{
    private const int CaptureRate = 48_000;

    /// <summary>Ten milliseconds at the capture rate. The APM's frame, and not negotiable.</summary>
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

    /// <summary>
    /// Less than a frame is held rather than forwarded. Padding it to length with silence would
    /// tell the canceller the Commander stopped talking, mid-word, several times a second.
    /// </summary>
    [Fact]
    public void APartialFrameIsHeldBack()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame - 1]);

        Assert.Empty(next.Writes);
    }

    /// <summary>A whole frame goes on as a whole frame.</summary>
    [Fact]
    public void AFullFrameIsForwarded()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame]);

        Assert.Single(next.Writes);
        Assert.Equal(Frame, next.Writes[0].Length);
    }

    /// <summary>
    /// The remainder is carried into the next call rather than dropped. Two writes that each fall
    /// short still add up to one frame, which is the arrangement WASAPI actually produces.
    /// </summary>
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

    /// <summary>
    /// A long write is split into whole frames with the tail held. Nothing is dropped and nothing
    /// is padded — the count that comes out is the count that went in, rounded down to frames.
    /// </summary>
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

    /// <summary>
    /// <c>Reset</c> drops the part-filled frame. What was half-captured is no longer a thing the
    /// Commander finished saying, and carrying it into the next utterance would splice two
    /// sentences together across a device change.
    /// </summary>
    [Fact]
    public void ResetDropsThePartialFrame()
    {
        var next = new RecordingCaptureSink();
        using var canceller = Started(next, new FakeRenderTap());

        canceller.Write(new float[Frame - 10]);
        canceller.Reset();

        // Ten more would have completed the frame had the remainder survived. It did not, so
        // nothing is forwarded yet.
        canceller.Write(new float[10]);

        Assert.Empty(next.Writes);
    }

    /// <summary>
    /// A reference frame from the mixer is accepted and does not disturb the capture side. The
    /// mixer is 48 kHz stereo, so this is the shape the real tap produces.
    /// </summary>
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

    /// <summary>
    /// The reference is re-shaped from the frame rather than assumed, so a mixer that changes
    /// format is followed instead of silently cancelling nothing. Both shapes in one run, because
    /// the reshape only happens on a change.
    /// </summary>
    [Fact]
    public void AChangeOfReferenceFormatIsFollowed()
    {
        var next = new RecordingCaptureSink();
        var tap = new FakeRenderTap();
        using var canceller = Started(next, tap);

        tap.Raise(new RenderReferenceFrame(0, new byte[480 * 2 * 2], new AudioFormat(48_000, 2)));
        tap.Raise(new RenderReferenceFrame(480, new byte[441 * 2], new AudioFormat(44_100, 1)));

        // Still cleaning capture afterwards, which is the thing a bad reshape would break.
        canceller.Write(new float[Frame]);

        Assert.Single(next.Writes);
        Assert.True(canceller.IsActive);
    }

    /// <summary>
    /// Silence in is silence out. Not a strong claim about the filter — it is the claim that the
    /// framing, the planar conversion and the module call are wired up such that a frame survives
    /// the round trip at all, which a transposed buffer would not.
    /// </summary>
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

        // Several frames rather than one: the module ramps up, and the first frame out of a
        // freshly initialised APM says more about convergence than about the configuration.
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

    /// <summary>
    /// The noise-suppression row reaches the module, measured rather than assumed — everything
    /// else about that flag is a property of a config object nothing reads back.
    /// <para>
    /// A continuous sine is what WebRTC classifies as stationary room noise — a fan, a pump, a
    /// coil whine — so switching suppression on attenuates it markedly, which is the whole reason
    /// the row is on by default: the voice-activity gate decides how far a frame sits above the
    /// room, and removing the room is the most direct way to make that decision easier.
    /// </para>
    /// <para>
    /// Asserted as a <b>comparison between the two configurations</b> rather than against fixed
    /// levels, because the exact attenuation is WebRTC's business and moves with its version.
    /// Two things about that were surprising enough to be worth writing down: the tone is
    /// attenuated rather than removed, and the <em>first</em> frame out of a freshly initialised
    /// module is near-silent whatever the settings, which is convergence and not suppression.
    /// A one-frame probe therefore measures the ramp-up, not the configuration.
    /// </para>
    /// </summary>
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

        // Present with the row off, and not amplified: both gain controllers are deliberately
        // off, so a build that turned one on would show up here too.
        Assert.InRange(withoutSuppression, 0.05f, 0.75f);

        // And meaningfully quieter with it on. Half is a wide margin against a measured ratio of
        // roughly a quarter, so this fails on the row being ignored rather than on a tuning change.
        Assert.True(
            withSuppression < withoutSuppression * 0.5f,
            $"suppression on measured {withSuppression:0.0000}, off measured {withoutSuppression:0.0000}");
    }
}
