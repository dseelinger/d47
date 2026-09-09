using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The continuous-listening gate policy, driven with synthesised audio and no microphone.
/// </summary>
public class VoiceActivityTests
{
    private const int Rate = 16000;

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    /// <summary>Room tone: quiet, steady, and not silence.</summary>
    private static float[] Room(TimeSpan duration, float level = 0.002f) => Noise(duration, level, seed: 7);

    /// <summary>Somebody talking: loud, and modulated the way speech is.</summary>
    private static float[] Speech(TimeSpan duration, float level = 0.2f)
    {
        var samples = new float[(int)(duration.TotalSeconds * Rate)];

        for (var i = 0; i < samples.Length; i++)
        {
            var envelope = 0.5f + (0.5f * MathF.Sin(2f * MathF.PI * 4f * i / Rate));

            samples[i] = level * envelope * (
                MathF.Sin(2f * MathF.PI * 220f * i / Rate)
                + (0.6f * MathF.Sin(2f * MathF.PI * 1_100f * i / Rate)));
        }

        return samples;
    }

    private static float[] Noise(TimeSpan duration, float level, int seed)
    {
        var random = new Random(seed);
        var samples = new float[(int)(duration.TotalSeconds * Rate)];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)((random.NextDouble() - 0.5) * 2 * level);
        }

        return samples;
    }

    private static ListenGate Gate(ListenMode mode = ListenMode.VoiceActivity) =>
        new(Rate, NullLogger<ListenGate>.Instance) { Mode = mode, Capturing = true, EchoCancelled = true };

    /// <summary>
    /// Writes audio and then polls, which is the arrangement in the running app: the detector decides
    /// on the audio thread and the tick thread carries it out.
    /// </summary>
    private static void Feed(ListenGate gate, float[] samples, ref DateTimeOffset now)
    {
        gate.Write(samples);
        now = now.AddSeconds((double)samples.Length / Rate);
        gate.Poll(now);
    }

    [Fact]
    public void TheRoomOnItsOwnNeverOpensTheGate()
    {
        var gate = Gate();
        var now = Start;
        var captures = 0;
        gate.Captured += _ => captures++;

        // A minute of a quiet room in front of an open microphone.
        for (var second = 0; second < 60; second++)
        {
            Feed(gate, Room(TimeSpan.FromSeconds(1)), ref now);
        }

        Assert.Equal(0, captures);
        Assert.False(gate.IsListening);
        Assert.Equal(MicrophoneState.Armed, gate.State);
    }

    [Fact]
    public void SpeechOpensTheGateAndTheSilenceAfterItClosesIt()
    {
        var gate = Gate();
        var now = Start;
        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);
        Assert.False(gate.IsListening);

        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);
        Assert.True(gate.IsListening);
        Assert.Equal(MicrophoneState.Open, gate.State);

        // Shorter than the hangover: still talking, as far as the gate is concerned.
        Feed(gate, Room(TimeSpan.FromMilliseconds(300)), ref now);
        Assert.True(gate.IsListening);
        Assert.Null(captured);

        Feed(gate, Room(TimeSpan.FromMilliseconds(800)), ref now);

        Assert.False(gate.IsListening);
        Assert.NotNull(captured);
        Assert.Equal(MicrophoneState.Armed, gate.State);
    }

    [Fact]
    public void APauseMidSentenceDoesNotCutTheUtteranceInHalf()
    {
        var gate = Gate();
        var now = Start;
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromMilliseconds(700)), ref now);

        // The moment a Commander stops to look at something.
        Feed(gate, Room(TimeSpan.FromMilliseconds(350)), ref now);
        Feed(gate, Speech(TimeSpan.FromMilliseconds(700)), ref now);
        Feed(gate, Room(TimeSpan.FromSeconds(1)), ref now);

        Assert.Single(captures);
        Assert.True(captures[0].Duration > TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void TheFrontOfTheFirstWordSurvivesTheOnsetDelay()
    {
        var gate = Gate();
        gate.PreRoll = TimeSpan.FromMilliseconds(500);
        var now = Start;
        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);

        // The detector will not call this speech until 60 ms of it has gone by, and the poll that acts on it
        // is later still.
        Feed(gate, Speech(TimeSpan.FromMilliseconds(600)), ref now);
        Feed(gate, Room(TimeSpan.FromSeconds(1)), ref now);

        Assert.NotNull(captured);
        Assert.True(
            captured!.Duration > TimeSpan.FromMilliseconds(600),
            $"Captured only {captured.Duration.TotalMilliseconds:0} ms of a 600 ms utterance plus its pre-roll.");
    }

    [Fact]
    public void ADetectorThatHasNotHeardTheRoomYetStaysShut()
    {
        var gate = Gate();
        var now = Start;
        var captures = 0;
        gate.Captured += _ => captures++;

        // The device's own startup transient, arriving as the very first thing.
        Feed(gate, Speech(TimeSpan.FromMilliseconds(200), level: 0.9f), ref now);

        Assert.False(gate.IsListening);
        Assert.Equal(0, captures);
    }

    [Fact]
    public void TurningTheSensitivityUpMakesQuietSpeechStopOpeningIt()
    {
        var now = Start;
        var loud = Gate();
        var deaf = Gate();
        deaf.Voice.Sensitivity = 30;

        foreach (var gate in new[] { loud, deaf })
        {
            var at = now;
            Feed(gate, Room(TimeSpan.FromSeconds(2)), ref at);
            Feed(gate, Speech(TimeSpan.FromMilliseconds(500), level: 0.02f), ref at);
        }

        // The same audio, one number apart.
        Assert.True(loud.IsListening);
        Assert.False(deaf.IsListening);
    }

    [Fact]
    public void TheKeyStillWorksInAHandsFreeMode()
    {
        var gate = Gate();
        var now = Start;
        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        gate.KeyDown(now);
        gate.Write(Room(TimeSpan.FromMilliseconds(400)));
        gate.KeyUp();

        // A Commander who wants to be certain d47 is listening should not have to trust a detector to agree —
        // so the key opens the gate on room tone the detector ignored.
        Assert.NotNull(captured);
    }

    [Fact]
    public void ADetectorEndDoesNotCloseAGateTheKeyOpened()
    {
        var gate = Gate();
        var now = Start;
        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);

        gate.KeyDown(now);

        // Long enough for the hangover to run out several times over.
        Feed(gate, Room(TimeSpan.FromSeconds(4)), ref now);

        Assert.True(gate.IsListening);
        Assert.Empty(captures);

        gate.KeyUp();
        Assert.Single(captures);
    }

    /// <summary>The half-duplex fallback.</summary>
    [Fact]
    public void WithNothingCancellingTheEchoTheGateWillNotOpenWhileD47IsTalking()
    {
        var gate = Gate();
        gate.EchoCancelled = false;
        var now = Start;
        var captures = 0;
        gate.Captured += _ => captures++;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);

        gate.FarEndActive = true;
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);

        Assert.False(gate.IsListening);
        Assert.Equal(0, captures);

        gate.FarEndActive = false;
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);

        Assert.True(gate.IsListening);
    }

    [Fact]
    public void WithTheEchoCancelledTheCommanderCanTalkOverD47()
    {
        var gate = Gate();
        var now = Start;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);

        // The whole point of Phase 13's first item.
        gate.FarEndActive = true;
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);

        Assert.True(gate.IsListening);
    }

    [Fact]
    public void PushToTalkNeverConsultsTheDetector()
    {
        var gate = Gate(ListenMode.PushToTalk);
        var now = Start;
        var captures = 0;
        gate.Captured += _ => captures++;

        // Shouting at a push-to-talk gate opens nothing, which is what push-to-talk means.
        for (var second = 0; second < 5; second++)
        {
            Feed(gate, Speech(TimeSpan.FromSeconds(1), level: 0.8f), ref now);
        }

        Assert.False(gate.IsListening);
        Assert.Equal(0, captures);
        Assert.Equal(MicrophoneState.Idle, gate.State);
    }

    [Fact]
    public void AClosedDeviceReportsItselfClosedRatherThanQuiet()
    {
        var gate = Gate();
        gate.Capturing = false;

        // The distinction the indicator exists to make: nothing is open, as against open and waiting.
        Assert.Equal(MicrophoneState.Off, gate.State);
    }

    [Fact]
    public void TheIndicatorFollowsEveryTransition()
    {
        var gate = Gate();
        var now = Start;
        var seen = new List<MicrophoneState>();
        gate.StateChanged += seen.Add;

        Feed(gate, Room(TimeSpan.FromSeconds(2)), ref now);
        Feed(gate, Speech(TimeSpan.FromSeconds(1)), ref now);
        Feed(gate, Room(TimeSpan.FromSeconds(1)), ref now);

        Assert.Equal([MicrophoneState.Open, MicrophoneState.Armed], seen);
    }

    [Fact]
    public void ChangingDeviceForgetsTheRoomItHadLearned()
    {
        var gate = Gate();
        var now = Start;

        // A loud room, learned.
        Feed(gate, Room(TimeSpan.FromSeconds(3), level: 0.15f), ref now);
        Assert.True(gate.Voice.NoiseFloor > -40);

        gate.Reset();

        Assert.False(gate.Voice.Settled);
        Assert.True(gate.Voice.NoiseFloor < -90);
    }
}
