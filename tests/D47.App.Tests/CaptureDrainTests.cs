using D47.Audio;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The drain that runs on NAudio's capture thread for every buffer the microphone delivers.
/// It used to read until the resampler returned nothing, which a ReadFully provider never does,
/// so it never returned at all — and a callback that never returns is a capture thread that
/// cannot be joined, which is what froze the app when push-to-talk was unbound.
/// </summary>
public class CaptureDrainTests
{
    private static readonly WaveFormat Device = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

    private static readonly WaveFormat Wanted =
        WaveFormat.CreateIeeeFloatWaveFormat(WasapiMicrophone.SampleRate, 1);

    /// <summary>Half a second of 16 kHz mono float, comfortably past the gate's minimum.</summary>
    private const long HalfASecond = WasapiMicrophone.SampleRate / 2 * sizeof(float);

    /// <summary>
    /// A BufferedWaveProvider with its default ReadFully, which is exactly what the microphone
    /// feeds the resampler. Without a budget this call does not come back.
    /// </summary>
    [Fact]
    public void AProviderThatNeverReportsEmptyStillTerminates()
    {
        var padding = new BufferedWaveProvider(Wanted) { BufferDuration = TimeSpan.FromSeconds(2) };

        Assert.True(padding.ReadFully, "the failure this guards depends on ReadFully being the default");

        var gate = Gate();

        WasapiMicrophone.Drain(padding, HalfASecond, gate);

        // Exactly the half second that was asked for, not an endless stream of silence.
        Assert.Equal(WasapiMicrophone.SampleRate / 2, Written(gate));
    }

    /// <summary>
    /// The fact the bug rested on, pinned so it cannot quietly change underneath the fix: with
    /// ReadFully set — the default, and what the microphone gets — a read from a provider that
    /// has never been given a single sample still returns a full buffer. The old drain looped
    /// while Read returned more than zero, so this is a loop with no exit.
    /// </summary>
    [Fact]
    public void AReadFullyProviderNeverReportsEmpty()
    {
        var padding = new BufferedWaveProvider(Wanted) { BufferDuration = TimeSpan.FromSeconds(2) };
        var buffer = new byte[4096];

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            Assert.Equal(buffer.Length, padding.Read(buffer, 0, buffer.Length));
        }
    }

    /// <summary>A provider that does report empty stops early rather than being padded up.</summary>
    [Fact]
    public void ADrainStopsWhenTheProviderRunsOut()
    {
        var scarce = new BufferedWaveProvider(Wanted)
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            ReadFully = false,
        };

        // Half of what the budget allows.
        scarce.AddSamples(new byte[HalfASecond / 2], 0, (int)(HalfASecond / 2));

        var gate = Gate();

        WasapiMicrophone.Drain(scarce, HalfASecond, gate);

        Assert.Equal(WasapiMicrophone.SampleRate / 4, Written(gate));
    }

    /// <summary>
    /// The budget converts between the device's rate and Whisper's. 48 kHz in, 16 kHz out, both
    /// mono float: a third as many bytes come out as went in.
    /// </summary>
    [Fact]
    public void TheBudgetIsWhatTheInputIsWorthAfterResampling()
    {
        Assert.Equal(4000L, WasapiMicrophone.OutputBytesFor(12000, Device, Wanted));
        Assert.Equal(0L, WasapiMicrophone.OutputBytesFor(0, Device, Wanted));
    }

    /// <summary>
    /// The signal reaches the gate intact, rather than as a fragment followed by silence.
    /// <para>
    /// This is the second bug ReadFully caused. The first was the endless loop the budget
    /// fixed; this one is quieter and worse. A read for more than has been buffered comes back
    /// padded with manufactured silence, the resampler converts that silence as if it were
    /// signal, and a second of continuous speech reached the gate as 10 ms of speech and 990 ms
    /// of nothing. Whisper still recognised the loudest surviving words, so it presented as
    /// "it cuts off the last thing I say" — audio present, transcript thin — rather than as a
    /// microphone that plainly did not work.
    /// </para>
    /// <para>
    /// Constant DC is the probe because any near-zero sample in the output is then provably
    /// fabricated. Both channel counts run: a stereo capture device is the common case and the
    /// downmix is where a channel-conversion bug would hide.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheSignalArrivesWholeRatherThanPaddedWithSilence(int channels)
    {
        MediaFoundationApi.Startup();

        var deviceFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, channels);

        // Configured exactly as WasapiMicrophone configures it; ReadFully is the property
        // under test, so this mirrors the real one rather than sharing it.
        var incoming = new BufferedWaveProvider(deviceFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2),
            ReadFully = false,
        };

        using var resampler = new MediaFoundationResampler(incoming, Wanted) { ResamplerQuality = 60 };

        var gate = new ListenGate(WasapiMicrophone.SampleRate, NullLogger<ListenGate>.Instance)
        {
            PreRoll = TimeSpan.Zero,
            MinimumLength = TimeSpan.Zero,
        };

        gate.KeyDown(DateTimeOffset.UnixEpoch);

        const int FramesPerCallback = 480;   // 10 ms at 48 kHz, as a capture device delivers
        var loud = new float[FramesPerCallback * channels];
        Array.Fill(loud, 0.5f);

        var chunk = new byte[loud.Length * sizeof(float)];
        Buffer.BlockCopy(loud, 0, chunk, 0, chunk.Length);

        for (var callback = 0; callback < 100; callback++)
        {
            incoming.AddSamples(chunk, 0, chunk.Length);

            WasapiMicrophone.Drain(
                resampler,
                WasapiMicrophone.OutputBytesFor(chunk.Length, deviceFormat, resampler.WaveFormat),
                gate);
        }

        float[] captured = [];
        gate.Captured += utterance => captured = utterance.Samples;
        gate.KeyUp();

        var fabricated = captured.Count(sample => MathF.Abs(sample) < 0.01f);

        Assert.True(
            captured.Length > WasapiMicrophone.SampleRate * 0.9,
            $"a second of audio should arrive as a second, not {captured.Length / 16000.0:0.00}s");

        Assert.True(
            fabricated == 0,
            $"{fabricated / 16.0:0.#} ms of the second reached the gate as silence that was never spoken");
    }

    private static ListenGate Gate()
    {
        var gate = new ListenGate(WasapiMicrophone.SampleRate, NullLogger<ListenGate>.Instance);

        // Open, so writes accumulate into an utterance that can be counted rather than being
        // absorbed by the pre-roll ring.
        gate.KeyDown(DateTimeOffset.UnixEpoch);
        return gate;
    }

    private static int Written(ListenGate gate)
    {
        var captured = 0;
        gate.Captured += utterance => captured = utterance.Samples.Length;
        gate.KeyUp();
        return captured;
    }
}
