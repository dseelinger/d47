using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The primary and comms push-to-talks share one gate: the key that opens a capture names who it is for,
/// and the other key does nothing until it closes.
/// </summary>
public sealed class EachPushToTalkOpensItsOwnCaptureTests
{
    private const int Rate = 16000;

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static ListenGate Gate(ListenMode mode) =>
        new(Rate, NullLogger<ListenGate>.Instance) { Mode = mode, Capturing = true, EchoCancelled = true };

    private static float[] Room(TimeSpan duration)
    {
        var random = new Random(7);
        var samples = new float[(int)(duration.TotalSeconds * Rate)];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)((random.NextDouble() - 0.5) * 2 * 0.002f);
        }

        return samples;
    }

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

    private static void Feed(ListenGate gate, float[] samples, ref DateTimeOffset now)
    {
        gate.Write(samples);
        now = now.AddSeconds((double)samples.Length / Rate);
        gate.Poll(now);
    }

    [Theory]
    [InlineData(ListenMode.PushToTalk, PttTarget.Core)]
    [InlineData(ListenMode.PushToTalk, PttTarget.Comms)]
    [InlineData(ListenMode.VoiceActivity, PttTarget.Comms)]
    [InlineData(ListenMode.WakeWord, PttTarget.Comms)]
    public void EachKeyHeldOpensACaptureForItsOwnTarget(ListenMode mode, PttTarget target)
    {
        var gate = Gate(mode);
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        gate.KeyDown(Start, target);
        gate.Write(Room(TimeSpan.FromMilliseconds(500)));
        gate.KeyUp(target);

        Assert.False(gate.IsListening);
        Assert.Equal(target, Assert.Single(captures).Target);
    }

    [Theory]
    [InlineData(PttTarget.Core, PttTarget.Comms)]
    [InlineData(PttTarget.Comms, PttTarget.Core)]
    public void TheOtherKeyDoesNothingWhileACaptureIsOpen(PttTarget holding, PttTarget other)
    {
        var gate = Gate(ListenMode.PushToTalk);
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        gate.KeyDown(Start, holding);
        gate.Write(Room(TimeSpan.FromMilliseconds(300)));

        gate.KeyDown(Start, other);
        gate.Write(Room(TimeSpan.FromMilliseconds(300)));
        gate.KeyUp(other);

        Assert.True(gate.IsListening);
        Assert.Empty(captures);

        gate.KeyUp(holding);

        Assert.Equal(holding, Assert.Single(captures).Target);
        Assert.Equal(TimeSpan.FromMilliseconds(600), captures[0].Duration);
    }

    [Fact]
    public void TheCommsKeyTakesOverACaptureVoiceOpened()
    {
        var gate = Gate(ListenMode.VoiceActivity);
        var now = Start;
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);
        Assert.True(gate.IsListening);

        gate.KeyDown(now, PttTarget.Comms);

        // Long enough for the detector's hangover to have closed a capture it still held.
        Feed(gate, Room(TimeSpan.FromSeconds(3)), ref now);
        Assert.True(gate.IsListening);

        gate.KeyUp(PttTarget.Comms);

        Assert.Equal(PttTarget.Comms, Assert.Single(captures).Target);
    }

    [Fact]
    public void ACaptureTheDetectorOpensAfterACommsCaptureIsForTheCore()
    {
        var gate = Gate(ListenMode.VoiceActivity);
        var now = Start;
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);

        gate.KeyDown(now, PttTarget.Comms);
        Feed(gate, Room(TimeSpan.FromMilliseconds(500)), ref now);
        gate.KeyUp(PttTarget.Comms);

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);
        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);

        Assert.Equal([PttTarget.Comms, PttTarget.Core], captures.Select(capture => capture.Target));
    }

    [Fact]
    public void InToggleModeTheCommsKeyTogglesAndThePrimaryDoesNotCloseIt()
    {
        var gate = Gate(ListenMode.Toggle);
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        gate.KeyDown(Start, PttTarget.Comms);
        gate.KeyUp(PttTarget.Comms);
        gate.Write(Room(TimeSpan.FromMilliseconds(500)));

        Assert.True(gate.IsListening);

        gate.KeyDown(Start, PttTarget.Core);
        gate.KeyUp(PttTarget.Core);

        Assert.True(gate.IsListening);
        Assert.Empty(captures);

        gate.KeyDown(Start, PttTarget.Comms);

        Assert.False(gate.IsListening);
        Assert.Equal(PttTarget.Comms, Assert.Single(captures).Target);
    }
}
