using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using Xunit;

namespace D47.App.Tests.Books;

public sealed class ABookPausesOnItsSentenceTests
{
    [Fact]
    public void APushToTalkPauseGoesOnFromTheSameSentenceOnceTheReplyEnds()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Finish(BookRig.First);

        rig.Audio.TurnInFlight = true;
        rig.Player.Pause(StoryRunPause.PushToTalk);
        rig.Settle();
        rig.Reply("reply");

        Assert.Equal(StoryRunPause.PushToTalk, rig.Player.Now!.Pause);

        rig.Audio.TurnInFlight = false;
        rig.Settle();

        Assert.True(rig.Player.Now!.IsPaused);

        rig.Finish("reply");

        Assert.False(rig.Player.Now!.IsPaused);
        Assert.Equal(BookRig.Second, rig.Player.Now.Text);

        rig.Sink.FinishUntil(BookRig.Second);

        Assert.Equal([BookRig.First, BookRig.Second, "reply", BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ACommanderPauseStaysPausedAfterTheReplyEnds()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Finish(BookRig.First);

        rig.Player.Pause(StoryRunPause.Commander);
        rig.Settle();
        rig.Reply("reply");
        rig.Finish("reply");
        rig.Player.Settled();
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal([BookRig.First, BookRig.Second, "reply"], rig.Sink.Names);

        rig.Player.Resume();
        rig.Settle();
        rig.Sink.FinishUntil(BookRig.Second);

        Assert.Equal([BookRig.First, BookRig.Second, "reply", BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ACommanderPauseIsNotTurnedIntoAPushToTalkPause()
    {
        var rig = BookRig.InMemory();
        rig.Play();

        rig.Player.Pause(StoryRunPause.Commander);
        rig.Player.Pause(StoryRunPause.PushToTalk);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
    }

    [Fact]
    public void AnAlertPauseGoesOnFromTheSameSentenceOnceTheAlertEnds()
    {
        var rig = BookRig.InMemory();
        rig.Play();

        rig.Audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Alert,
            Clip = new AudioClip("alert", new byte[960], AudioFormat.Standard),
            Group = "alert",
        });
        rig.Settle();

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);

        rig.Finish("alert");

        Assert.False(rig.Player.Now!.IsPaused);

        rig.Sink.FinishUntil(BookRig.First);

        Assert.Equal([BookRig.First, "alert", BookRig.First], rig.Sink.Names);
    }

    [Fact]
    public void AVoiceThatThrowsPausesOnItsSentenceWithTheErrorAndDoesNotMoveOn()
    {
        var rig = BookRig.InMemory();
        rig.Voices["kokoro"].Fails = BookRig.Second;
        rig.Play();

        rig.Finish(BookRig.First);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal(BookRig.Second, rig.Player.Now.Text);
        Assert.Equal("model file not found", rig.Player.Now.Problem);
        Assert.Equal([BookRig.First], rig.Sink.Names);

        rig.Player.Settled();
        rig.Settle();

        Assert.Equal([BookRig.First], rig.Sink.Names);

        rig.Voices["kokoro"].Fails = null;
        rig.Player.Resume();
        rig.Settle();

        Assert.Null(rig.Player.Now!.Problem);
        Assert.Equal([BookRig.First, BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ASentenceNotMadeInTimeIsReportedAsWaiting()
    {
        var rig = BookRig.InMemory();
        var voice = rig.Voices["kokoro"];
        voice.Holds = true;

        rig.Play();

        Assert.True(rig.Player.Now!.Waiting);
        Assert.Equal([BookRig.First], voice.Said);

        voice.Release(BookRig.First);
        rig.Settle();

        Assert.False(rig.Player.Now!.Waiting);
        Assert.Equal([BookRig.First], rig.Sink.Names);

        rig.Finish(BookRig.First);

        Assert.True(rig.Player.Now!.Waiting);
        Assert.Equal(BookRig.Second, rig.Player.Now.Text);

        voice.Release(BookRig.Second);
        rig.Settle();

        Assert.False(rig.Player.Now!.Waiting);
        Assert.Equal([BookRig.First, BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ThePlaceIsSavedAtEachNewParagraphAndTheEndMarksTheBookFinished()
    {
        var rig = BookRig.InMemory();
        rig.Play();

        rig.Finish(BookRig.First);
        Assert.Equal(new BookPlace(0, 0), rig.Shelf.Find(BookRig.Source)!.Place);

        rig.Finish(BookRig.Second);
        Assert.Equal(new BookPlace(0, 1), rig.Shelf.Find(BookRig.Source)!.Place);

        rig.Finish(BookRig.Third);
        Assert.Equal(new BookPlace(1, 0), rig.Shelf.Find(BookRig.Source)!.Place);

        rig.Finish(BookRig.Fourth);

        Assert.Null(rig.Player.Now);
        Assert.True(rig.Shelf.Find(BookRig.Source)!.Finished);
    }
}
