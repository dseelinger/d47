using D47.App.Voice;
using D47.Core.Audio;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>Cancel or "stop" over a callout leaves the book it paused to read on; over the book alone it is the Commander's pause (#2777).</summary>
public sealed class CancelLeavesABookACalloutPausedToGoOnTests
{
    [Fact]
    public void ABookACalloutPausedKeepsThatPauseAndReadsItsSentenceAgainOnceSettled()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Finish(BookRig.First);
        rig.Say(AudioChannel.Speech, "callout", "callout-1");
        rig.Sink.FinishUntil("callout");

        CommanderStop.Silence(rig.Audio, rig.Story, rig.Player);

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);

        rig.Player.Settled();
        rig.Settle();

        Assert.False(rig.Player.Now!.IsPaused);
        Assert.Equal(BookRig.Second, rig.Player.Now.Text);

        rig.Sink.FinishUntil(BookRig.Second);

        Assert.Equal([BookRig.First, BookRig.Second, "callout", BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ABookBeingReadWithNothingElsePlayingIsPausedAsTheCommandersPause()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Finish(BookRig.First);

        CommanderStop.Silence(rig.Audio, rig.Story, rig.Player);
        rig.Settle();
        rig.Player.Settled();
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal([BookRig.First, BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ABookPausedOnlyForPushToTalkIsPausedAsTheCommandersPause()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Finish(BookRig.First);
        rig.Audio.TurnInFlight = true;
        rig.Player.Pause(StoryRunPause.PushToTalk);
        rig.Settle();

        CommanderStop.Silence(rig.Audio, rig.Story, rig.Player);
        rig.Audio.TurnInFlight = false;
        rig.Player.Settled();
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
    }
}
