using D47.Audio;
using D47.Core.Audio;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Echo cancellation (list.md Phase 13), driven with synthesised audio and no hardware.
/// <para>
/// This is the one part of Phase 13 that cannot live in Core — it is a native library, so it
/// sits behind the same project boundary NAudio and Whisper do (architecture.md §3). What can
/// still be asserted without a microphone or a speaker is everything that matters: that a far
/// end played into the reference and heard back through a simulated room comes out subtracted,
/// that the Commander talking over it survives, and that a canceller which cannot start hands
/// capture through untouched rather than taking the microphone down with it.
/// </para>
/// </summary>
public class EchoCancellationTests
{
    private const int CaptureRate = 16_000;
    private const int RenderRate = 48_000;

    /// <summary>
    /// A test double for the arbiter's tap. The real one is a pass-through in the mixer graph;
    /// what this class needs from it is only the shape.
    /// </summary>
    private sealed class Tap : IRenderReferenceTap
    {
        public event Action<RenderReferenceFrame>? Rendered;

        public void Play(float[] samples, int channels)
        {
            var pcm = new byte[samples.Length * 2];

            for (var i = 0; i < samples.Length; i++)
            {
                var value = (short)Math.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                pcm[i * 2] = (byte)value;
                pcm[(i * 2) + 1] = (byte)(value >> 8);
            }

            Rendered?.Invoke(new RenderReferenceFrame(0, pcm, new AudioFormat(RenderRate, channels)));
        }
    }

    /// <summary>Collects what came out the far side, which is what the gate would have seen.</summary>
    private sealed class Collector : ICaptureSink
    {
        public List<float> Samples { get; } = [];

        public int Resets { get; private set; }

        public void Write(ReadOnlySpan<float> samples) => Samples.AddRange(samples);

        public void Reset() => Resets++;
    }

    /// <summary>
    /// d47 talking, as broadband non-periodic sound rather than as tones.
    /// <para>
    /// The distinction is not cosmetic and it cost an afternoon to find. An adaptive filter is
    /// excited by broadband signal, and AEC3's delay estimator correlates band energies to find
    /// where the echo sits — against a sum of steady sine waves that correlation has a hundred
    /// equally good answers, and measured here, a harmonic far end drops the cancellation from
    /// thirty-odd decibels to nothing. Speech is broadband, so this is the faithful stand-in as
    /// well as the one the module can do anything with.
    /// </para>
    /// <para>
    /// Hashed from the sample index rather than drawn from a <c>Random</c>, so the reference and
    /// the echo agree on it sample for sample without either having to store it.
    /// </para>
    /// </summary>
    private static float Far(long at)
    {
        var x = ((ulong)at + 1) * 6364136223846793005UL + 1442695040888963407UL;
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdUL;
        x ^= x >> 33;

        return 0.3f * (float)(((x >> 40) / 8388608.0) - 1.0);
    }

    /// <summary>The Commander talking: a different voice, with a syllable-rate envelope.</summary>
    private static float Near(long at)
    {
        var seconds = (float)at / CaptureRate;
        var envelope = 0.5f + (0.5f * MathF.Sin(2f * MathF.PI * 4f * seconds));

        return 0.15f * envelope * (
            MathF.Sin(2f * MathF.PI * 220f * seconds)
            + (0.6f * MathF.Sin(2f * MathF.PI * 1_100f * seconds)));
    }

    private static double Energy(IEnumerable<float> samples) =>
        samples.Aggregate(0d, (sum, sample) => sum + ((double)sample * sample));

    private static double Db(double of, double against) =>
        10 * Math.Log10((of + 1e-12) / (against + 1e-12));

    /// <summary>
    /// A room where d47 is playing through speakers and the microphone hears it 60 ms later at a
    /// third of the level — optionally with the Commander talking over the top.
    /// <para>
    /// It runs continuously so a test can play a stretch to let the filter converge and then
    /// measure the next one: convergence is a real property of every echo canceller ever built,
    /// and hiding it inside the measurement measures the wrong thing.
    /// </para>
    /// </summary>
    private sealed class Speakers(EchoCanceller canceller, Tap tap, Collector gate)
    {
        private const int RenderFrame = RenderRate / 100;
        private const int CaptureFrame = CaptureRate / 100;

        /// <summary>60 ms of air between the speaker and the microphone, at the render rate.</summary>
        private const int Delay = RenderRate * 60 / 1000;

        private readonly float[] _render = new float[RenderFrame * 2];
        private readonly float[] _capture = new float[CaptureFrame];

        private long _renderAt;
        private long _captureAt;

        /// <summary>What the microphone would have heard from D47 alone, for comparison.</summary>
        public List<float> EchoOnly { get; } = [];

        /// <summary>What the Commander actually said, at the same offsets.</summary>
        public List<float> NearOnly { get; } = [];

        /// <summary>Runs <paramref name="seconds"/> and returns what reached the gate in them.</summary>
        public List<float> Play(double seconds, bool commanderTalking)
        {
            var from = gate.Samples.Count;
            var echoFrom = EchoOnly.Count;

            for (var frame = 0; frame < (int)(seconds * 100); frame++)
            {
                for (var i = 0; i < RenderFrame; i++)
                {
                    var sample = Far(_renderAt++);
                    _render[i * 2] = sample;
                    _render[(i * 2) + 1] = sample;
                }

                tap.Play(_render, channels: 2);

                for (var i = 0; i < CaptureFrame; i++)
                {
                    // The capture index maps to the render index three to one, then back by the
                    // delay — which is what makes this an echo of exactly what was played.
                    var source = (_captureAt * 3) - Delay;
                    var echo = source < 0 ? 0f : 0.33f * Far(source);
                    var near = commanderTalking ? Near(_captureAt) : 0f;

                    EchoOnly.Add(echo);
                    NearOnly.Add(near);
                    _capture[i] = echo + near;
                    _captureAt++;
                }

                canceller.Write(_capture);
            }

            _ = echoFrom;
            return gate.Samples[from..];
        }

        /// <summary>The echo and the near end over the same stretch the last <c>Play</c> returned.</summary>
        public (List<float> Echo, List<float> Near) Since(int samples) =>
            (EchoOnly[^samples..], NearOnly[^samples..]);
    }

    private static EchoCanceller Build(Tap tap, Collector gate) =>
        new(gate, tap, CaptureRate, NullLogger<EchoCanceller>.Instance);

    /// <summary>
    /// A canceller with the noise suppressor switched off, for the two tests that measure how
    /// much echo was removed.
    /// <para>
    /// Not because the suppressor is unwanted — it ships on — but because it would be measured
    /// here instead of the thing under test. It is built to remove steady tonal sound and keep
    /// speech, and the synthetic near end below is a pair of sine waves: exactly what it is for.
    /// Left on, it removes 12 dB of the Commander's stand-in voice and the test reads that as the
    /// canceller having gone half-duplex, which is the opposite of true.
    /// </para>
    /// </summary>
    private static EchoCanceller Measuring(Tap tap, Collector gate)
    {
        var canceller = Build(tap, gate);
        canceller.SuppressNoise = false;
        return canceller;
    }

    /// <summary>
    /// The claim the whole item rests on. Without this, continuous listening on speakers is d47
    /// hearing itself, transcribing itself and answering itself.
    /// </summary>
    [Fact]
    public void WhatD47PlaysIsSubtractedFromWhatItHears()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Measuring(tap, gate);
        canceller.Start();

        Assert.True(canceller.IsActive, canceller.Unavailable ?? "The canceller did not start.");

        var speakers = new Speakers(canceller, tap, gate);

        // Half a second for the filter to converge, which is a real property of every echo
        // canceller ever built rather than something to hide inside the measurement.
        speakers.Play(seconds: 0.5, commanderTalking: false);

        var heard = speakers.Play(seconds: 2, commanderTalking: false);
        var (echo, _) = speakers.Since(heard.Count);

        var attenuation = Db(Energy(echo), Energy(heard));

        Assert.True(
            attenuation > 20,
            $"Only {attenuation:0.#} dB of echo was removed, which is not enough to keep D47 from "
            + "hearing itself.");
    }

    /// <summary>
    /// "Natural interruption", which is the checklist's own phrase for it. An echo canceller
    /// that solved the problem by going deaf while D47 speaks would pass the test above and
    /// deliver none of the point of it.
    /// </summary>
    [Fact]
    public void TheCommanderCanTalkOverIt()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Measuring(tap, gate);
        canceller.Start();

        var speakers = new Speakers(canceller, tap, gate);

        speakers.Play(seconds: 0.5, commanderTalking: false);

        var heard = speakers.Play(seconds: 2, commanderTalking: true);
        var (echo, near) = speakers.Since(heard.Count);

        // Some loss is inherent and is not the failure being guarded against: AEC3 applies a
        // residual suppressor while the far end is active, and a few decibels off the near end
        // during double-talk is what every echo canceller in existence does. What would be a
        // failure is the near end being gone — so the bar is set where "attenuated" stops and
        // "erased" starts, well clear of both.
        var kept = Db(Energy(heard), Energy(near));

        Assert.True(
            kept > -10,
            $"The Commander's own voice came through {kept:0.#} dB down while D47 was speaking, "
            + "which is a canceller that has gone half-duplex rather than one that cancels.");

        // Paired with WhatD47PlaysIsSubtractedFromWhatItHears, which measures the other half on
        // the same room: the echo goes and the Commander stays. Neither claim is worth much
        // without the other — a canceller can pass either one alone by doing nothing, or by
        // muting capture.
        Assert.NotEmpty(echo);
    }

    /// <summary>
    /// Frames arrive from WASAPI at whatever size it feels like, and the module takes exactly
    /// 10 ms. Everything the Commander said has to survive that, in order.
    /// </summary>
    [Fact]
    public void AudioIsNotLostAcrossOddlySizedBuffers()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Build(tap, gate);
        canceller.Start();

        var sizes = new[] { 37, 480, 1, 160, 999, 2_048 };
        var written = 0;

        foreach (var size in sizes)
        {
            canceller.Write(new float[size]);
            written += size;
        }

        // Whole frames only, with the remainder held for the next call — padding a short frame
        // with silence would tell the canceller the Commander stopped talking, mid-word, several
        // times a second.
        var frame = CaptureRate / 100;

        Assert.Equal(written / frame * frame, gate.Samples.Count);
    }

    /// <summary>
    /// A capability being off, not a startup failure (list.md Phase 3). A machine where the
    /// native library will not load is a machine that still has push-to-talk.
    /// </summary>
    [Fact]
    public void WithTheCancellerStoppedCaptureStillReachesTheGateUntouched()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Build(tap, gate);

        var samples = new float[512];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f;
        }

        // Never started. Every buffer goes straight through, which is exactly what happened
        // before this class existed.
        canceller.Write(samples);

        Assert.False(canceller.IsActive);
        Assert.Equal(512, gate.Samples.Count);
        Assert.All(gate.Samples, sample => Assert.Equal(0.5f, sample));
    }

    [Fact]
    public void ItCanBeStartedAndStoppedRepeatedly()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Build(tap, gate);

        // Applying any listening setting runs this path, so it happens on every change of every
        // row — the same lifecycle mistake the microphone and the transcriber both had.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            canceller.Start();
            Assert.True(canceller.IsActive);
            canceller.Stop();
            Assert.False(canceller.IsActive);
        }

        canceller.Start();
        canceller.Start();

        Assert.True(canceller.IsActive);
    }

    /// <summary>
    /// Stopping runs on whichever thread applied a settings row; the capture and render threads
    /// are inside the module continuously. Freeing its handle while one of them is in there is a
    /// native access violation rather than an exception, and no <c>catch</c> anywhere would see
    /// it — the process simply goes.
    /// <para>
    /// A race is never proved absent by a test. What this does is drive the arrangement hard
    /// enough that an unsynchronised version fails loudly here rather than quietly on a
    /// Commander's machine, at the one moment they touched a settings row.
    /// </para>
    /// </summary>
    [Fact]
    public async Task StoppingWhileAudioIsFlowingDoesNotTakeTheProcessDown()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Build(tap, gate);
        canceller.Start();

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var capture = Task.Run(
            () =>
            {
                var samples = new float[160];

                while (!stop.IsCancellationRequested)
                {
                    canceller.Write(samples);
                }
            },
            TestContext.Current.CancellationToken);

        var render = Task.Run(
            () =>
            {
                var samples = new float[RenderRate / 100 * 2];

                while (!stop.IsCancellationRequested)
                {
                    tap.Play(samples, channels: 2);
                }
            },
            TestContext.Current.CancellationToken);

        // The settings thread, doing what applying a listening row does.
        while (!stop.IsCancellationRequested)
        {
            canceller.Stop();
            canceller.Start();
        }

        await Task.WhenAll(capture, render);

        Assert.True(canceller.IsActive);
        Assert.NotEmpty(gate.Samples);
    }

    [Fact]
    public void ADeviceGoingAwayResetsTheFramingAndTellsTheGate()
    {
        var tap = new Tap();
        var gate = new Collector();
        using var canceller = Build(tap, gate);
        canceller.Start();

        // Half a frame, held back waiting for the rest of it.
        canceller.Write(new float[80]);
        Assert.Empty(gate.Samples);

        canceller.Reset();

        // The held half belonged to a device that is no longer the one being heard, and joining
        // it to the front of the next device's first frame would be a click at best.
        canceller.Write(new float[80]);

        Assert.Equal(1, gate.Resets);
        Assert.Empty(gate.Samples);
    }
}
