using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>Records what the arbiter starts and stops, and finishes a clip only when told to.</summary>
internal sealed class SteppedSink : IAudioSink
{
    private readonly List<long> _done = [];

    public List<PlaybackRequest> Started { get; } = [];

    public event Action<long>? Finished;

    public IRenderReferenceTap ReferenceTap { get; } = new SilentTap();

    /// <summary>The names of every clip started, the arbiter's gaps left out.</summary>
    public List<string> Names => [.. Started.Select(request => request.Name).Where(name => name != "spoken-gap")];

    public void Play(PlaybackRequest request) => Started.Add(request);

    /// <summary>Lets the clip playing now, if any, run to its end.</summary>
    public void FinishCurrent()
    {
        if (Started.LastOrDefault(request => !_done.Contains(request.Id)) is { } playing)
        {
            _done.Add(playing.Id);
            Finished?.Invoke(playing.Id);
        }
    }

    /// <summary>Finishes clips until the one named <paramref name="name"/> is playing, gaps included.</summary>
    public void FinishUntil(string name)
    {
        for (var guard = 0; guard < 20 && Started.LastOrDefault(request => !_done.Contains(request.Id))?.Name != name; guard++)
        {
            FinishCurrent();
        }
    }

    public void Stop(long playbackId) => _done.Add(playbackId);

    public void Pause(long playbackId)
    {
    }

    public void Resume(long playbackId)
    {
    }

    public void SetGain(long playbackId, float gain)
    {
    }

    public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false)
    {
    }

    public TrackProgress? Progress(long playbackId) => null;

    public bool Seek(long playbackId, TimeSpan to) => false;
}

public sealed class PlayAllPlaysAStoryRunInOrderTests
{
    private static readonly Story Run = new() { Id = "quiet-relay", Title = "The Quiet Relay", PublicLayer = "-", PickedAt = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero) };

    /// <summary>Five lines: the second keeps no clip and the fourth's clip cannot be read.</summary>
    private static readonly StoryRunLine[] Lines =
    [
        Line(0, "first", clip: true),
        Line(1, "no clip", clip: false),
        Line(2, "third", clip: true),
        Line(3, "unreadable", clip: true),
        Line(4, "fifth", clip: true),
    ];

    private static StoryRunLine Line(int minute, string body, bool clip) => new()
    {
        Sent = Run.PickedAt.AddMinutes(minute),
        From = "Narrator",
        Body = body,
        Key = $"line.{minute}",
        Clip = clip ? $"{minute}.wav" : null,
    };

    private sealed record Rig(AudioArbiter Audio, SteppedSink Sink, StoryRunPlayer Player, Queue<Action> Background)
    {
        /// <summary>Runs what the player sent off its thread, and anything that sends.</summary>
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
    }

    private static Rig Build()
    {
        var sink = new SteppedSink();
        var audio = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var background = new Queue<Action>();
        var player = new StoryRunPlayer(
            audio,
            (_, line) => line.Body == "unreadable" ? null : new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            background.Enqueue);

        return new Rig(audio, sink, player, background);
    }

    private static AudioRequest Reply(string name) =>
        new() { Channel = AudioChannel.Speech, Clip = new AudioClip(name, new byte[960], AudioFormat.Standard), Group = "turn-1" };

    [Fact]
    public void ItPlaysEachKeptClipInOrderAndSkipsLinesItCannotPlay()
    {
        var rig = Build();

        rig.Player.Play(Run, Lines, 0);
        rig.Settle();

        rig.Finish();
        Assert.Equal(2, rig.Player.Now!.Index);
        rig.Finish();
        rig.Finish();

        Assert.Equal(["first", "third", "fifth"], rig.Sink.Names);
        Assert.Null(rig.Player.Now);
    }

    [Fact]
    public void ItStartsFromTheLineChosen()
    {
        var rig = Build();

        rig.Player.Play(Run, Lines, 2);
        rig.Settle();

        Assert.Equal(["third"], rig.Sink.Names);
    }

    [Fact]
    public void PushToTalkPausesItAndPlayResumesFromThePausedLine()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 0);
        rig.Settle();
        rig.Finish();

        rig.Player.Pause(StoryRunPause.PushToTalk);
        rig.Audio.Enqueue(Reply("the reply"));
        rig.Sink.FinishUntil("the reply");

        Assert.Equal(StoryRunPause.PushToTalk, rig.Player.Now!.Pause);

        rig.Finish();
        rig.Player.Resume();
        rig.Settle();
        rig.Sink.FinishUntil("third");

        Assert.Equal(["first", "third", "the reply", "third"], rig.Sink.Names);
        Assert.False(rig.Player.Now!.IsPaused);
    }

    [Fact]
    public void ACommanderPauseOnAPlaybackPausedForPushToTalkReplacesTheReason()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 0);
        rig.Settle();
        rig.Player.Pause(StoryRunPause.PushToTalk);

        var changed = 0;
        rig.Player.Changed += () => changed++;
        rig.Player.Pause(StoryRunPause.Commander);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal(1, changed);

        rig.Player.Pause(StoryRunPause.Commander);
        rig.Player.Pause(StoryRunPause.PushToTalk);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void APauseJustAfterALineFinishedResumesAtTheNextLine()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 0);
        rig.Settle();

        rig.Sink.FinishCurrent();
        rig.Player.Pause(StoryRunPause.PushToTalk);
        rig.Settle();
        rig.Player.Resume();
        rig.Settle();

        Assert.Equal(["first", "third"], rig.Sink.Names);
        Assert.Equal(2, rig.Player.Now!.Index);
    }

    [Fact]
    public void AnAlertPausesItAndItStaysPausedUntilPlay()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 0);
        rig.Settle();

        rig.Audio.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = new AudioClip("interdiction", new byte[960], AudioFormat.Standard) });
        rig.Finish();
        rig.Finish();

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);
        Assert.Equal(0, rig.Player.Now.Index);
        Assert.Equal(["first", "interdiction"], rig.Sink.Names);
    }

    [Fact]
    public void SkipPlaysTheNextLine()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 2);
        rig.Settle();

        rig.Player.Skip();
        rig.Settle();

        Assert.Equal(["third", "fifth"], rig.Sink.Names);
        Assert.Equal(4, rig.Player.Now!.Index);
    }

    [Fact]
    public void StopEndsItAndClearsThePlayingLine()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 0);
        rig.Settle();

        rig.Player.Stop();
        rig.Settle();
        rig.Audio.Enqueue(Reply("the reply"));

        Assert.Null(rig.Player.Now);
        Assert.Equal(["first", "the reply"], rig.Sink.Names);
    }

    [Fact]
    public void AClipReadAfterStopIsNotPlayed()
    {
        var rig = Build();

        rig.Player.Play(Run, Lines, 0);
        rig.Player.Stop();
        rig.Settle();

        Assert.Empty(rig.Sink.Names);
    }

    [Fact]
    public void AClipReadAfterSkipIsNotPlayedAsWell()
    {
        var rig = Build();

        rig.Player.Play(Run, Lines, 0);
        rig.Player.Skip();
        rig.Settle();

        Assert.Equal(["third"], rig.Sink.Names);
    }

    [Fact]
    public void ASilenceAloneLeavesItPausedAndTheStopThatComesWithItEndsIt()
    {
        var rig = Build();
        rig.Player.Play(Run, Lines, 0);
        rig.Settle();

        rig.Audio.Silence();
        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);

        // The shut-up control: AppHost.CancelNow stops the player before it silences.
        rig.Player.Stop();
        rig.Audio.Silence();

        Assert.Null(rig.Player.Now);
    }
}
