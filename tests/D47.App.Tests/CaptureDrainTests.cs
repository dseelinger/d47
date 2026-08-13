using D47.Audio;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
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
