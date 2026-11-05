using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>
/// During Play all a line with no clip is made in the speaker's voice, the next one while the line before it plays, and
/// a line that cannot be spoken is held for half a second a word, at least four seconds, before playback moves on.
/// </summary>
public sealed class PlayAllSpeaksAgainOrHoldsALineWithNoClipTests
{
    private static readonly Story Run = new() { Id = "quiet-relay", Title = "The Quiet Relay", PublicLayer = "-", PickedAt = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero) };

    private static StoryRunLine Line(int minute, string body, bool clip) => new()
    {
        Sent = Run.PickedAt.AddMinutes(minute),
        From = "narrator",
        Body = body,
        Key = $"line.{minute}",
        Clip = clip ? $"{minute}.wav" : null,
    };

    private static AudioClip Clip(string name) => new(name, new byte[960], AudioFormat.Standard);

    private sealed record Rig(AudioArbiter Audio, SteppedSink Sink, StoryRunPlayer Player, Queue<Action> Background, Clock Time, Dictionary<string, TaskCompletionSource<SpokenAgain>> Asked)
    {
        public void Settle()
        {
            while (Background.TryDequeue(out var work))
            {
                work();
            }
        }

        public void Finish()
        {
            Sink.FinishCurrent();
            Settle();
        }

        public void Answer(string body, SpokenAgain spoken)
        {
            Asked[body].SetResult(spoken);
            Settle();
        }
    }

    private static Rig Build()
    {
        var sink = new SteppedSink();
        var audio = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var background = new Queue<Action>();
        var time = new Clock();
        var asked = new Dictionary<string, TaskCompletionSource<SpokenAgain>>(StringComparer.Ordinal);
        var player = new StoryRunPlayer(
            audio,
            (_, line) => Clip(line.Body),
            background.Enqueue,
            (_, line, _) =>
            {
                if (!asked.TryGetValue(line.Body, out var answer))
                {
                    asked[line.Body] = answer = new TaskCompletionSource<SpokenAgain>();
                }

                return answer.Task;
            },
            time);

        return new Rig(audio, sink, player, background, time, asked);
    }

    [Fact]
    public void TheNextLineWithNoClipIsMadeBeforeTheLinePlayingEnds()
    {
        var rig = Build();
        StoryRunLine[] lines = [Line(0, "first", clip: true), Line(1, "kept", clip: true), Line(2, "spoken again", clip: false)];

        rig.Player.Play(Run, lines, 0);
        rig.Settle();

        Assert.Equal(["first"], rig.Sink.Names);
        Assert.True(rig.Asked.ContainsKey("spoken again"));

        rig.Asked["spoken again"].SetResult(new SpokenAgain(Clip("spoken again")));
        rig.Finish();
        rig.Finish();

        Assert.Equal(["first", "kept", "spoken again"], rig.Sink.Names);
        Assert.Single(rig.Asked);
    }

    [Fact]
    public void ALineBeingMadeShowsSpeakingAgainThenPlays()
    {
        var rig = Build();

        rig.Player.Play(Run, [Line(0, "spoken again", clip: false), Line(1, "after", clip: true)], 0);
        rig.Settle();

        Assert.Equal(StoryRunLineState.SpeakingAgain, rig.Player.Now!.State);
        Assert.Empty(rig.Sink.Names);

        rig.Answer("spoken again", new SpokenAgain(Clip("spoken again")));

        Assert.Equal(StoryRunLineState.Playing, rig.Player.Now!.State);
        Assert.Equal(["spoken again"], rig.Sink.Names);
    }

    [Fact]
    public void AFailingLineIsHeldForFourSecondsAtLeastThenPlayAllMovesOn()
    {
        var rig = Build();

        rig.Player.Play(Run, [Line(0, "three short words", clip: false), Line(1, "after", clip: true)], 0);
        rig.Settle();
        rig.Answer("three short words", SpokenAgain.Held(StoryLineHold.Failed, "HTTP 503 · ELEVENLABS"));

        var held = rig.Player.Now!;
        Assert.Equal(StoryRunLineState.Held, held.State);
        Assert.Equal(StoryLineHold.Failed, held.Hold);
        Assert.Equal("HTTP 503 · ELEVENLABS", held.HoldDetail);
        Assert.Equal(rig.Time.GetUtcNow() + TimeSpan.FromSeconds(4), held.HeldUntil);

        rig.Time.Advance(TimeSpan.FromSeconds(3.9));
        rig.Settle();
        Assert.Equal(0, rig.Player.Now!.Index);
        Assert.Empty(rig.Sink.Names);

        rig.Time.Advance(TimeSpan.FromSeconds(0.1));
        rig.Settle();
        Assert.Equal(1, rig.Player.Now!.Index);
        Assert.Equal(["after"], rig.Sink.Names);
    }

    [Fact]
    public void ALongLineIsHeldHalfASecondAWord()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), StoryRunPlayer.ReadingTime("one two three"));
        Assert.Equal(TimeSpan.FromSeconds(4), StoryRunPlayer.ReadingTime("one two three four five six seven eight"));
        Assert.Equal(TimeSpan.FromSeconds(6), StoryRunPlayer.ReadingTime(string.Join(' ', Enumerable.Repeat("word", 12))));
    }

    [Fact]
    public void PauseStopsTheCountdownAndSkipMovesOnFromAHeldLine()
    {
        var rig = Build();
        var words = string.Join(' ', Enumerable.Repeat("word", 12));

        rig.Player.Play(Run, [Line(0, words, clip: false), Line(1, "after", clip: true)], 0);
        rig.Settle();
        rig.Answer(words, SpokenAgain.Held(StoryLineHold.NoProvider));

        rig.Player.Pause(StoryRunPause.Commander);
        rig.Time.Advance(TimeSpan.FromSeconds(10));
        rig.Settle();
        Assert.Equal(0, rig.Player.Now!.Index);

        rig.Player.Resume();
        rig.Player.Skip();
        rig.Settle();

        Assert.Equal(1, rig.Player.Now!.Index);
        Assert.Equal(["after"], rig.Sink.Names);
    }

    /// <summary>A clock that moves only when told to, firing the timers that fall due.</summary>
    private sealed class Clock : TimeProvider
    {
        private readonly List<Timer> _timers = [];
        private DateTimeOffset _now = new(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(callback, state, _now + dueTime);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;

            foreach (var due in _timers.Where(timer => timer.Due is { } at && at <= _now).ToList())
            {
                due.Due = null;
                due.Callback(due.State);
            }
        }

        private sealed class Timer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public DateTimeOffset? Due { get; set; } = due;

            public TimerCallback Callback => callback;

            public object? State => state;

            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose() => Due = null;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
