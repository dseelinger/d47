using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Subtitle cues in the caption band, following a video's position, and D47's lines arriving during one.</summary>
public class ASubtitleSharesTheCaptionBandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 21, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<SubtitleCue> Cues =
    [
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "The reactor is stable for now.\nWe have about ten minutes."),
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8), "Copy that."),
        new(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(7), "Houston, go ahead."),
        new(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30),
            "Telemetry shows the second stage separating cleanly, and the third stage igniting on time over the range."),
    ];

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    /// <summary>The first cue as the caption rules wrap it.</summary>
    private static readonly IReadOnlyList<string> Reactor = Caption.Wrap("The reactor is stable for now. We have about ten minutes.");

    private static CaptionLayer Following(bool pausesForSpeech = true)
    {
        var layer = new CaptionLayer { VideoPausesForSpeech = pausesForSpeech };
        layer.Follow(Cues);
        return layer;
    }

    [Fact]
    public void TheCueCoveringThePositionIsShownWrappedByTheCaptionRules()
    {
        var layer = Following();

        layer.At(TimeSpan.FromSeconds(2));

        Assert.Equal(Reactor, layer.Lines);
        Assert.Null(layer.Above);
        Assert.True(layer.Visible);
        Assert.All(layer.Lines, line => Assert.True(line.Length <= Caption.CharactersPerLine));
    }

    [Fact]
    public void APositionBetweenCuesShowsNothing()
    {
        var layer = Following();

        layer.At(TimeSpan.FromSeconds(2));
        layer.At(TimeSpan.FromSeconds(4.5));

        Assert.Empty(layer.Lines);
        Assert.False(layer.Visible);
    }

    [Fact]
    public void TheCueClearsAtItsEnd()
    {
        var layer = Following();

        layer.At(TimeSpan.FromSeconds(3.9));
        Assert.NotEmpty(layer.Lines);

        layer.At(TimeSpan.FromSeconds(4));
        Assert.Empty(layer.Lines);
    }

    [Fact]
    public void OverlappingCuesShowTheLaterOne()
    {
        var layer = Following();

        layer.At(TimeSpan.FromSeconds(6.5));

        Assert.Equal(["Houston, go ahead."], layer.Lines);
    }

    [Fact]
    public void ABackwardsSeekShowsTheRightCueAtOnce()
    {
        var layer = Following();

        layer.At(TimeSpan.FromSeconds(7.5));
        Assert.Equal(["Copy that."], layer.Lines);

        layer.At(TimeSpan.FromSeconds(1.5));
        Assert.Equal(Reactor, layer.Lines);
    }

    [Fact]
    public void APausedPositionKeepsItsCueAndRaisesNothingFurther()
    {
        var layer = Following();
        var changes = 0;
        layer.Changed += () => changes++;

        for (var tick = 0; tick < 20; tick++)
        {
            layer.At(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(1, changes);
        Assert.NotEmpty(layer.Lines);
    }

    [Fact]
    public void ACueLongerThanTheWindowShowsTheWindowsWorth()
    {
        var layer = Following();

        layer.At(TimeSpan.FromSeconds(25));

        Assert.InRange(layer.Lines.Count, 1, Caption.WindowLines);
    }

    [Fact]
    public void FollowingNothingClearsTheCue()
    {
        var layer = Following();
        layer.At(TimeSpan.FromSeconds(2));

        layer.Follow(null);
        layer.At(TimeSpan.FromSeconds(2));

        Assert.Empty(layer.Lines);
    }

    [Fact]
    public void CaptionsTurnedOffHideTheSubtitleToo()
    {
        var layer = Following();
        layer.Settings = new CaptionSettings { Enabled = false };

        layer.At(TimeSpan.FromSeconds(2));

        Assert.False(layer.Visible);
    }

    [Fact]
    public void CuesDoNotFeedTheSpeakingSpeed()
    {
        var layer = Following();
        var now = Now;

        for (var at = TimeSpan.Zero; at < TimeSpan.FromSeconds(40); at += TimeSpan.FromMilliseconds(100))
        {
            layer.At(at);
            layer.Tick(now);
            now += TimeSpan.FromMilliseconds(100);
        }

        Assert.Equal(CaptionLayer.InitialSpeakingSpeed, layer.SpeakingSpeed);
    }

    [Fact]
    public void WithTheVideoPausingD47sLineTakesTheBandAndTheCueReturnsWhenD47Finishes()
    {
        var layer = Following(pausesForSpeech: true);
        layer.At(TimeSpan.FromSeconds(2));

        layer.Say("Interdiction attempt detected, Commander.", Now);

        Assert.Equal(["Interdiction attempt detected, Commander."], layer.Lines);
        Assert.Null(layer.Above);

        // The video is paused while D47 speaks, so the position stands still.
        layer.At(TimeSpan.FromSeconds(2));
        layer.Tick(Now + Second);
        layer.At(TimeSpan.FromSeconds(2));
        layer.Quiet(Now + (Second * 3));
        layer.At(TimeSpan.FromSeconds(2));

        Assert.Equal(["Interdiction attempt detected, Commander."], layer.Lines);

        // The voice has stopped and the video plays on.
        layer.At(TimeSpan.FromSeconds(2.1));

        Assert.Equal(Reactor, layer.Lines);
        Assert.Null(layer.Above);
    }

    [Fact]
    public void WithTheVideoPausedForOtherReasonsTheCueReturnsWhenD47sLinesClear()
    {
        var layer = Following(pausesForSpeech: true);
        layer.At(TimeSpan.FromSeconds(2));

        layer.Say("Fuel is at nineteen per cent.", Now);
        layer.Quiet(Now + (Second * 2));

        for (var tick = 1; tick <= 100; tick++)
        {
            layer.At(TimeSpan.FromSeconds(2));
            layer.Tick(Now + (Second * 2) + TimeSpan.FromMilliseconds(100 * tick));
        }

        Assert.Equal(Reactor, layer.Lines);
    }

    [Fact]
    public void WithTheVideoPlayingOnD47sLineSitsAboveTheCue()
    {
        var layer = Following(pausesForSpeech: false);
        layer.At(TimeSpan.FromSeconds(2));

        layer.Say("Interdiction attempt detected, Commander.", Now);

        Assert.Equal("Interdiction attempt detected, Commander.", layer.Above);
        Assert.Equal(Reactor, layer.Lines);

        // The cue moves on underneath it.
        layer.At(TimeSpan.FromSeconds(5.5));

        Assert.Equal("Interdiction attempt detected, Commander.", layer.Above);
        Assert.Equal(["Copy that."], layer.Lines);
    }

    [Fact]
    public void WithTheVideoPlayingOnD47sLineLeavesWhenItsDwellEnds()
    {
        var layer = Following(pausesForSpeech: false);
        layer.At(TimeSpan.FromSeconds(2));

        layer.Say("Fuel is at nineteen per cent.", Now);
        layer.Quiet(Now + (Second * 2));
        layer.Tick(Now + (Second * 2) + Caption.MaximumDwell);

        Assert.Null(layer.Above);
        Assert.Equal(Reactor, layer.Lines);
    }

    [Fact]
    public void WithTheVideoPlayingOnALongerD47LineTakesTheBandAlone()
    {
        var layer = Following(pausesForSpeech: false);
        layer.At(TimeSpan.FromSeconds(2));

        layer.Say("Interdiction attempt detected, Commander. Submit or run, but decide now.", Now);

        Assert.Null(layer.Above);
        Assert.Equal(2, layer.Lines.Count);
        Assert.DoesNotContain(Reactor[^1], layer.Lines);

        // Once D47's lines clear, the cue for wherever the video has got to is back.
        layer.Quiet(Now + (Second * 4));
        layer.Tick(Now + (Second * 4) + Caption.MaximumDwell);
        layer.At(TimeSpan.FromSeconds(5.5));

        Assert.Equal(["Copy that."], layer.Lines);
    }

    [Fact]
    public void WithTheVideoPlayingOnAThreeLineCueLeavesNoRoomAndD47TakesTheBandAlone()
    {
        var layer = new CaptionLayer { VideoPausesForSpeech = false };
        layer.Follow(
        [
            new SubtitleCue(
                TimeSpan.Zero,
                TimeSpan.FromSeconds(10),
                "Telemetry shows the second stage separating cleanly, the third stage igniting on time, and the vehicle downrange."),
        ]);
        layer.At(TimeSpan.FromSeconds(1));
        Assert.Equal(Caption.WindowLines, layer.Lines.Count);

        layer.Say("Fuel is at nineteen per cent.", Now);

        Assert.Null(layer.Above);
        Assert.Equal(["Fuel is at nineteen per cent."], layer.Lines);
    }

    [Fact]
    public void SilencingD47LeavesTheCue()
    {
        var layer = Following(pausesForSpeech: false);
        layer.At(TimeSpan.FromSeconds(2));
        layer.Say("Fuel is at nineteen per cent.", Now);
        var changes = 0;
        layer.Changed += () => changes++;

        layer.Silence();

        Assert.Equal(1, changes);
        Assert.Null(layer.Above);
        Assert.Equal(Reactor, layer.Lines);
    }
}
