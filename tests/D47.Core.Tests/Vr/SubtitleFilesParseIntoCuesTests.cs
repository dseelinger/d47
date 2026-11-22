using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>SRT and WebVTT files read into timed cues.</summary>
public class SubtitleFilesParseIntoCuesTests
{
    /// <summary>An SRT file as NASA publishes one: a byte-order mark, CRLF endings, numbered blocks.</summary>
    private const string Srt =
        "﻿1\r\n"
        + "00:00:01,000 --> 00:00:04,500\r\n"
        + "The reactor is stable for now.\r\n"
        + "We have about ten minutes.\r\n"
        + "\r\n"
        + "2\r\n"
        + "00:00:05,250 --> 00:00:07,000\r\n"
        + "<i>Copy that,</i> {\\an8}<b>Houston</b>.\r\n"
        + "\r\n"
        + "3\r\n"
        + "01:02:03.004 --> 01:02:05.000\r\n"
        + "Range &amp; rate look good.\r\n";

    private const string Vtt =
        "WEBVTT - Launch\n"
        + "Kind: captions\n"
        + "\n"
        + "STYLE\n"
        + "::cue { color: yellow }\n"
        + "\n"
        + "NOTE Recorded at the pad.\n"
        + "A second line of the note.\n"
        + "\n"
        + "REGION\n"
        + "id:bottom width:40%\n"
        + "\n"
        + "intro\n"
        + "00:01.500 --> 00:03.000 align:center line:90%\n"
        + "<v Flight>Go for <c.loud>launch</c>.</v>\n"
        + "\n"
        + "00:00:04.000 --> 00:00:06.000\n"
        + "<00:04.500>Liftoff &lt;now&gt;.\n";

    [Fact]
    public void AnSrtFileWithAByteOrderMarkAndCrlfEndingsParses()
    {
        var cues = SubtitleCues.Parse(Srt);

        Assert.Equal(
            [
                new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4.5), "The reactor is stable for now.\nWe have about ten minutes."),
                new SubtitleCue(TimeSpan.FromSeconds(5.25), TimeSpan.FromSeconds(7), "Copy that, Houston."),
                new SubtitleCue(
                    new TimeSpan(0, 1, 2, 3, 4),
                    new TimeSpan(0, 1, 2, 5, 0),
                    "Range & rate look good."),
            ],
            cues);
    }

    [Fact]
    public void AWebVttFileParsesWithItsHeaderBlocksSettingsAndTagsLeftOut()
    {
        var cues = SubtitleCues.Parse(Vtt);

        Assert.Equal(
            [
                new SubtitleCue(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3), "Go for launch."),
                new SubtitleCue(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), "Liftoff <now>."),
            ],
            cues);
    }

    [Fact]
    public void LoneCarriageReturnsSeparateLinesToo()
    {
        var cues = SubtitleCues.Parse("1\r00:00:01,000 --> 00:00:02,000\rOne.\r\r2\r00:00:03,000 --> 00:00:04,000\rTwo.\r");

        Assert.Equal(["One.", "Two."], cues.Select(cue => cue.Text));
    }

    [Fact]
    public void CuesComeOutInStartOrderWhateverOrderTheFileHasThem()
    {
        var cues = SubtitleCues.Parse(
            "2\n00:00:05,000 --> 00:00:06,000\nSecond.\n\n1\n00:00:01,000 --> 00:00:02,000\nFirst.\n");

        Assert.Equal(["First.", "Second."], cues.Select(cue => cue.Text));
    }

    [Theory]
    [InlineData("1\n00:00:01,000 --> 00:00:01,000\nNo length.\n")]
    [InlineData("1\n00:00:03,000 --> 00:00:02,000\nBackwards.\n")]
    [InlineData("1\n00:00:xx,000 --> 00:00:02,000\nNot a time.\n")]
    [InlineData("1\n00:00:01,000 --> \nNo end.\n")]
    [InlineData("1\n00:00:01,000 --> 00:00:02,000\n<i></i>\n")]
    [InlineData("Just some text\nwith no timing line.\n")]
    [InlineData("")]
    public void ABlockThatIsNotACueIsSkippedRatherThanThrown(string text)
    {
        Assert.Empty(SubtitleCues.Parse(text));
    }

    [Fact]
    public void AMalformedBlockLeavesTheCuesAroundIt()
    {
        var cues = SubtitleCues.Parse(
            "1\n00:00:01,000 --> 00:00:02,000\nKept.\n\n2\ngarbage --> more garbage\nLost.\n\n3\n00:00:03,000 --> 00:00:04,000\nAlso kept.\n");

        Assert.Equal(["Kept.", "Also kept."], cues.Select(cue => cue.Text));
    }

    [Fact]
    public void APositionBetweenCuesFindsNone()
    {
        var cues = SubtitleCues.Parse(Srt);

        Assert.Null(SubtitleCues.At(cues, TimeSpan.FromSeconds(4.8)));
        Assert.Null(SubtitleCues.At(cues, TimeSpan.FromSeconds(0.5)));
    }

    [Fact]
    public void ACueEndsJustBeforeItsEndTime()
    {
        var cues = SubtitleCues.Parse(Srt);

        Assert.Equal("Copy that, Houston.", SubtitleCues.At(cues, TimeSpan.FromSeconds(6.999))?.Text);
        Assert.Null(SubtitleCues.At(cues, TimeSpan.FromSeconds(7)));
    }

    [Fact]
    public void OverlappingCuesFindTheOneThatStartedLater()
    {
        var cues = SubtitleCues.Parse(
            "1\n00:00:01,000 --> 00:00:10,000\nLong.\n\n2\n00:00:03,000 --> 00:00:05,000\nShort.\n");

        Assert.Equal("Long.", SubtitleCues.At(cues, TimeSpan.FromSeconds(2))?.Text);
        Assert.Equal("Short.", SubtitleCues.At(cues, TimeSpan.FromSeconds(4))?.Text);
        Assert.Equal("Long.", SubtitleCues.At(cues, TimeSpan.FromSeconds(6))?.Text);
    }

    [Fact]
    public void CuesStartingTogetherFindTheLaterInTheFile()
    {
        var cues = SubtitleCues.Parse(
            "1\n00:00:01,000 --> 00:00:05,000\nFirst.\n\n2\n00:00:01,000 --> 00:00:03,000\nSecond.\n");

        Assert.Equal("Second.", SubtitleCues.At(cues, TimeSpan.FromSeconds(2))?.Text);
    }
}
