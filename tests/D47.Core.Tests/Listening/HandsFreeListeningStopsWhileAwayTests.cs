using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

public class HandsFreeListeningStopsWhileAwayTests
{
    private const int Rate = 16000;

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static float[] Speech(TimeSpan duration)
    {
        var samples = new float[(int)(duration.TotalSeconds * Rate)];

        for (var i = 0; i < samples.Length; i++)
        {
            var envelope = 0.5f + (0.5f * MathF.Sin(2f * MathF.PI * 4f * i / Rate));
            samples[i] = 0.2f * envelope * (
                MathF.Sin(2f * MathF.PI * 220f * i / Rate)
                + (0.6f * MathF.Sin(2f * MathF.PI * 1_100f * i / Rate)));
        }

        return samples;
    }

    private static float[] Quiet(TimeSpan duration)
    {
        var random = new Random(7);
        var samples = new float[(int)(duration.TotalSeconds * Rate)];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)((random.NextDouble() - 0.5) * 0.004);
        }

        return samples;
    }

    private static ListenGate Gate(Func<bool> stopped) =>
        new(Rate, NullLogger<ListenGate>.Instance)
        {
            Mode = ListenMode.VoiceActivity,
            Capturing = true,
            EchoCancelled = true,
            VoiceOpeningStopped = stopped,
        };

    private static void Feed(ListenGate gate, float[] samples, ref DateTimeOffset now)
    {
        gate.Write(samples);
        now = now.AddSeconds((double)samples.Length / Rate);
        gate.Poll(now);
    }

    [Fact]
    public void AVoiceOnsetDoesNotOpenTheGateWhileStopped()
    {
        var gate = Gate(() => true);
        var now = Start;
        var captures = 0;
        gate.Captured += _ => captures++;

        Feed(gate, Quiet(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);
        Feed(gate, Quiet(TimeSpan.FromSeconds(2)), ref now);

        Assert.False(gate.IsListening);
        Assert.Equal(0, captures);
    }

    [Fact]
    public void AKeyStillOpensTheGateWhileStopped()
    {
        var gate = Gate(() => true);
        var now = Start;
        Feed(gate, Quiet(TimeSpan.FromSeconds(1)), ref now);

        gate.KeyDown(now);

        Assert.True(gate.IsListening);
        Assert.Equal(MicrophoneState.Open, gate.State);
    }

    [Fact]
    public void AVoiceOnsetOpensTheGateWhenNotStopped()
    {
        var gate = Gate(() => false);
        var now = Start;

        Feed(gate, Quiet(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);

        Assert.True(gate.IsListening);
    }

    [Fact]
    public void ACaptureOpenWhenTheStopBeginsIsDelivered()
    {
        var stopped = false;
        var gate = Gate(() => stopped);
        var now = Start;
        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        Feed(gate, Quiet(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);
        Assert.True(gate.IsListening);

        stopped = true;
        Feed(gate, Quiet(TimeSpan.FromSeconds(2)), ref now);

        Assert.False(gate.IsListening);
        Assert.NotNull(captured);
    }

    [Fact]
    public void TheMicrophoneReadsIdleWhileStoppedAndArmedAgainOnReturn()
    {
        var stopped = false;
        var gate = Gate(() => stopped);
        var now = Start;
        var states = new List<MicrophoneState>();
        gate.StateChanged += states.Add;

        Feed(gate, Quiet(TimeSpan.FromSeconds(1)), ref now);
        Assert.Equal(MicrophoneState.Armed, gate.State);

        stopped = true;
        Feed(gate, Quiet(TimeSpan.FromSeconds(1)), ref now);
        Assert.Equal(MicrophoneState.Idle, gate.State);

        stopped = false;
        Feed(gate, Quiet(TimeSpan.FromSeconds(1)), ref now);
        Assert.Equal(MicrophoneState.Armed, gate.State);
        Assert.Equal([MicrophoneState.Idle, MicrophoneState.Armed], states);
    }
}
