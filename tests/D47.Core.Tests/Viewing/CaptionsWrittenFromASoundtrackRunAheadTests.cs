using D47.Core.Viewing;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary>
/// Captions written a stretch at a time: what each stretch keeps, where the next starts, and when the picture waits
/// for them (#2677).
/// </summary>
public class CaptionsWrittenFromASoundtrackRunAheadTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static SubtitleCue Cue(double start, double end, string text = "words") => new(S(start), S(end), text);

    [Fact]
    public void AStretchKeepsTheCuesThatEndBeforeTheOverlapAndTheNextStartsAtTheFirstItDropped()
    {
        var run = new CaptionRun(S(100));

        run.Accept(S(100), CaptionRun.Stretch, [Cue(1, 4, "one"), Cue(10, 24, "two"), Cue(23, 29, "cut off")], ended: false);

        Assert.Equal(["one", "two"], run.Cues.Select(cue => cue.Text));
        Assert.Equal(S(101), run.Cues[0].Start);
        Assert.Equal(S(123), run.WrittenTo);
        Assert.False(run.Finished);
    }

    [Fact]
    public void WithNothingDroppedTheNextStretchStartsAtTheOverlap()
    {
        var run = new CaptionRun(S(0));

        run.Accept(S(0), CaptionRun.Stretch, [Cue(2, 6)], ended: false);

        Assert.Equal(CaptionRun.Stretch - CaptionRun.Overlap, run.WrittenTo);
    }

    [Fact]
    public void OneLongCueCannotHoldWritingInPlace()
    {
        var run = new CaptionRun(S(0));

        run.Accept(S(0), CaptionRun.Stretch, [Cue(1, 28, "a long song")], ended: false);

        Assert.Single(run.Cues);
        Assert.Equal(S(28), run.WrittenTo);
    }

    [Fact]
    public void TheLastStretchKeepsEveryCueAndFinishes()
    {
        var run = new CaptionRun(S(0));

        run.Accept(S(0), S(12), [Cue(1, 4), Cue(9, 12)], ended: true);

        Assert.Equal(2, run.Cues.Count);
        Assert.Equal(S(12), run.WrittenTo);
        Assert.True(run.Finished);
        Assert.False(run.Behind(S(11.5)));
        Assert.True(run.CaughtUp(S(11.5)));
    }

    [Fact]
    public void AStretchLandingEarlyKeepsNoCueFromBeforeWhereWritingHadReached()
    {
        var run = new CaptionRun(S(0));
        run.Accept(S(0), CaptionRun.Stretch, [Cue(1, 4, "one")], ended: false);

        run.Accept(S(24.5), CaptionRun.Stretch, [Cue(0, 0.4, "again"), Cue(1, 3, "two")], ended: false);

        Assert.Equal(["one", "two"], run.Cues.Select(cue => cue.Text));
    }

    [Fact]
    public void ThePictureWaitsNearTheEndOfTheCaptionsAndPlaysOnOnceTheyAreWellAhead()
    {
        var run = new CaptionRun(S(0));

        Assert.True(run.Behind(S(0)));

        run.Accept(S(0), CaptionRun.Stretch, [], ended: false);

        Assert.False(run.Behind(S(20)));
        Assert.True(run.Behind(S(24.2)));
        Assert.True(run.CaughtUp(S(15)));
        Assert.False(run.CaughtUp(S(15.1)));
    }

    [Fact]
    public void WritingWaitsOnceItIsAMinuteAhead()
    {
        var run = new CaptionRun(S(0));
        run.Accept(S(0), CaptionRun.Stretch, [], ended: false);
        run.Accept(S(25), CaptionRun.Stretch, [], ended: false);
        run.Accept(S(50), CaptionRun.Stretch, [], ended: false);

        Assert.Equal(S(75), run.WrittenTo);
        Assert.True(run.Ahead(S(15)));
        Assert.False(run.Ahead(S(15.1)));
    }

    [Fact]
    public void APictureMovedOutsideTheCaptionsStartsWritingAgainThereAndKeepsWhatEndsBefore()
    {
        var run = new CaptionRun(S(60));
        run.Accept(S(60), CaptionRun.Stretch, [Cue(1, 4, "one"), Cue(10, 14, "two")], ended: false);

        Assert.False(run.Jumped(S(70)));
        Assert.True(run.Jumped(S(200)));
        Assert.True(run.Jumped(S(30)));

        run.Restart(S(65));

        Assert.Equal(["one"], run.Cues.Select(cue => cue.Text));
        Assert.Equal(S(65), run.WrittenTo);
        Assert.Equal(S(65), run.From);
    }

    [Fact]
    public void EachChangePublishesANewList()
    {
        var run = new CaptionRun(S(0));
        var before = run.Cues;

        run.Accept(S(0), CaptionRun.Stretch, [Cue(1, 4)], ended: false);

        Assert.NotSame(before, run.Cues);
        Assert.Empty(before);
    }
}
