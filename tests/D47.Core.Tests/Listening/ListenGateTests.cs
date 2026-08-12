using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Gate policy transitions with no microphone present (architecture.md §8). Every test feeds
/// samples straight in, which is the same thing a WAV file into the gate would do — the gate
/// takes audio and edges and knows nothing about where either came from.
/// </summary>
public class ListenGateTests
{
    private const int Rate = 16000;

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static ListenGate Gate(ListenMode mode = ListenMode.PushToTalk) =>
        new(Rate, NullLogger<ListenGate>.Instance) { Mode = mode };

    /// <summary>A block of samples all set to one value, so a test can tell blocks apart.</summary>
    private static float[] Block(TimeSpan duration, float value) =>
        [.. Enumerable.Repeat(value, (int)(duration.TotalSeconds * Rate))];

    [Fact]
    public void HoldingTheKeyCapturesWhatWasSaidWhileItWasHeld()
    {
        var gate = Gate();
        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromSeconds(1), 0.5f));
        gate.KeyUp();

        Assert.NotNull(captured);
        Assert.Equal(Rate, captured!.Samples.Length);
        Assert.Equal(TimeSpan.FromSeconds(1), captured.Duration);
    }

    [Fact]
    public void AudioFromBeforeTheKeyWasNoticedIsStillCaptured()
    {
        var gate = Gate();
        gate.PreRoll = TimeSpan.FromMilliseconds(500);

        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        // 200 ms of speech that happened before the tick loop sampled the key. Without the
        // pre-roll this is the front of the first word, and it is where the proper nouns are.
        gate.Write(Block(TimeSpan.FromMilliseconds(200), 0.25f));

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromMilliseconds(800), 0.75f));
        gate.KeyUp();

        Assert.NotNull(captured);
        Assert.Equal(TimeSpan.FromSeconds(1), captured!.Duration);

        // The early audio is at the front, in order, not merely present.
        Assert.Equal(0.25f, captured.Samples[0]);
        Assert.Equal(0.75f, captured.Samples[^1]);
    }

    [Fact]
    public void ThePreRollNeverGrowsPastItsWindow()
    {
        var gate = Gate();
        gate.PreRoll = TimeSpan.FromMilliseconds(500);

        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        // Ten seconds of room tone while nobody is talking to d47. A buffer that kept all of it
        // would be a memory leak that runs for as long as the session does.
        gate.Write(Block(TimeSpan.FromSeconds(10), 0.1f));

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromMilliseconds(500), 0.9f));
        gate.KeyUp();

        Assert.NotNull(captured);
        Assert.Equal(TimeSpan.FromSeconds(1), captured!.Duration);
    }

    [Fact]
    public void TheRingKeepsTheMostRecentAudioNotTheOldest()
    {
        var gate = Gate();
        gate.PreRoll = TimeSpan.FromMilliseconds(500);

        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        gate.Write(Block(TimeSpan.FromMilliseconds(500), 0.1f));   // old, should be gone
        gate.Write(Block(TimeSpan.FromMilliseconds(500), 0.2f));   // recent, should survive

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromMilliseconds(100), 0.9f));
        gate.KeyUp();

        Assert.NotNull(captured);
        Assert.All(captured!.Samples[..8000], sample => Assert.Equal(0.2f, sample));
    }

    [Fact]
    public void AudioArrivingWhileTheGateIsShutIsNotCapturedTwice()
    {
        var gate = Gate();
        gate.PreRoll = TimeSpan.FromMilliseconds(500);

        var captures = new List<Utterance>();
        gate.Captured += captures.Add;

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromSeconds(1), 0.5f));
        gate.KeyUp();

        gate.KeyDown(Start.AddSeconds(5));
        gate.Write(Block(TimeSpan.FromSeconds(1), 0.6f));
        gate.KeyUp();

        // The second utterance must not begin with the tail of the first. Opening clears the
        // ring precisely so a previous utterance cannot bleed into the next one.
        Assert.Equal(2, captures.Count);
        Assert.Equal(0.6f, captures[1].Samples[0]);
    }

    [Fact]
    public void AMisPressIsDiscardedRatherThanTranscribed()
    {
        var gate = Gate();
        gate.MinimumLength = TimeSpan.FromMilliseconds(250);

        var captured = false;
        UtteranceEnd? ended = null;
        gate.Captured += _ => captured = true;
        gate.Ended += reason => ended = reason;

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromMilliseconds(80), 0.5f));
        gate.KeyUp();

        // Transcribing 80 ms of room tone produces a confident wrong word, which is worse than
        // producing nothing at all.
        Assert.False(captured);
        Assert.Equal(UtteranceEnd.TooShort, ended);
    }

    [Fact]
    public void ToggleModeStartsOnOnePressAndStopsOnTheNext()
    {
        var gate = Gate(ListenMode.Toggle);

        Utterance? captured = null;
        gate.Captured += utterance => captured = utterance;

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromSeconds(1), 0.5f));

        // Releasing does nothing in toggle mode — that is the entire difference between the
        // two, and it is a policy over the same gate rather than a second mechanism.
        gate.KeyUp();
        Assert.True(gate.IsListening);
        Assert.Null(captured);

        gate.Write(Block(TimeSpan.FromSeconds(1), 0.5f));
        gate.KeyDown(Start.AddSeconds(2));

        Assert.False(gate.IsListening);
        Assert.NotNull(captured);
        Assert.Equal(TimeSpan.FromSeconds(2), captured!.Duration);
    }

    [Fact]
    public void AStuckKeyIsClosedAtTheCeilingAndTheAudioIsKept()
    {
        var gate = Gate();
        gate.MaximumLength = TimeSpan.FromSeconds(10);

        Utterance? captured = null;
        UtteranceEnd? ended = null;
        gate.Captured += utterance => captured = utterance;
        gate.Ended += reason => ended = reason;

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromSeconds(12), 0.5f));

        gate.Poll(Start.AddSeconds(5));
        Assert.True(gate.IsListening);

        gate.Poll(Start.AddSeconds(11));

        // Emitted rather than discarded: they said something, and it is better transcribed late
        // than lost.
        Assert.False(gate.IsListening);
        Assert.Equal(UtteranceEnd.TooLong, ended);
        Assert.NotNull(captured);
    }

    [Fact]
    public void TheGateReadsNoClockOfItsOwn()
    {
        var gate = Gate();
        gate.MaximumLength = TimeSpan.FromSeconds(10);

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromSeconds(1), 0.5f));

        // A year of wall-clock could pass here and nothing would happen, because the only time
        // the gate knows is the time it is handed.
        gate.Poll(Start.AddSeconds(1));

        Assert.True(gate.IsListening);
    }

    [Fact]
    public void AbandoningEmitsNothing()
    {
        var gate = Gate();

        var captured = false;
        gate.Captured += _ => captured = true;

        gate.KeyDown(Start);
        gate.Write(Block(TimeSpan.FromSeconds(2), 0.5f));
        gate.Abandon();

        Assert.False(gate.IsListening);
        Assert.False(captured);
    }

    [Fact]
    public void ReleasingWithoutPressingDoesNothing()
    {
        var gate = Gate();

        var captured = false;
        gate.Captured += _ => captured = true;

        gate.KeyUp();

        Assert.False(gate.IsListening);
        Assert.False(captured);
    }

    [Fact]
    public void StartedFiresOnceWhenTheGateOpens()
    {
        var gate = Gate();
        var starts = 0;
        gate.Started += () => starts++;

        gate.KeyDown(Start);
        gate.KeyDown(Start.AddMilliseconds(100));

        // Key repeat is not a second utterance.
        Assert.Equal(1, starts);
    }
}
