using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// The caption standard, asserted rather than claimed (list.md Phase 9, "Captions follow
/// Netflix CC standard").
/// </summary>
public class CaptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShortTextStaysOnOneLine()
    {
        // The standard is explicit that text stays on one line unless it exceeds the limit.
        Assert.Equal(["Fuel is at nineteen per cent."], Caption.Wrap("Fuel is at nineteen per cent."));
    }

    [Fact]
    public void NoLineExceedsTheStandardsLength()
    {
        var said =
            "Scan complete. Two high metal content worlds and a water world with a terraforming "
            + "candidate rating, about nine hundred light seconds out from the primary star.";

        var lines = Caption.Wrap(said);

        Assert.All(lines, line => Assert.True(
            line.Length <= Caption.CharactersPerLine,
            $"'{line}' is {line.Length} characters, over the {Caption.CharactersPerLine} limit"));
    }

    [Fact]
    public void WrappingLosesNoWords()
    {
        var said = "Interdiction detected. Submit or run, Commander, but decide now.";

        Assert.Equal(said, string.Join(' ', Caption.Wrap(said)));
    }

    /// <summary>
    /// Bottom-heavy when nothing better is competing. Filling the first line and letting the
    /// second take what is left is what a naive wrap does, and it is the shape the standard
    /// specifically asks against.
    /// </summary>
    [Fact]
    public void ATwoLineCaptionIsBottomHeavyWhenNothingElseDecidesTheBreak()
    {
        var lines = Caption.Wrap("Scanning that unidentified signal source nearby yields nothing useful");

        Assert.Equal(2, lines.Count);
        Assert.True(
            lines[1].Length >= lines[0].Length,
            $"top '{lines[0]}' ({lines[0].Length}) should not be longer than bottom '{lines[1]}' ({lines[1].Length})");
    }

    /// <summary>
    /// And when they compete, the syntactic break wins. The standard lists punctuation and
    /// conjunctions as where a break belongs and treats the shape as a preference, so a rule
    /// that put the shape first would break phrases apart to make the lines even.
    /// </summary>
    [Fact]
    public void ASyntacticBreakBeatsAMerelyBalancedOne()
    {
        var lines = Caption.Wrap("Docking permission granted, pad seven, and you have ten minutes.");

        Assert.Equal(2, lines.Count);
        Assert.EndsWith(",", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("and ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ALineBreaksBeforeAConjunctionRatherThanAfterIt()
    {
        var lines = Caption.Wrap("The frame shift drive is charging and the route is plotted now.");

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("and ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyOrWhitespaceSaysNothing()
    {
        Assert.Empty(Caption.Wrap(string.Empty));
        Assert.Empty(Caption.Wrap("   \n  "));
    }

    [Fact]
    public void DwellIsCharactersOverReadingSpeed()
    {
        // Forty characters at twenty a second is two seconds, which is inside both bounds.
        var text = new string('x', 40);

        Assert.Equal(2.0, Caption.DwellFor(text, Caption.AdultReadingSpeed).TotalSeconds, 3);
    }

    [Fact]
    public void DwellIsFlooredAndCeilingedByTheStandard()
    {
        Assert.Equal(Caption.MinimumDwell, Caption.DwellFor("Go.", Caption.AdultReadingSpeed));
        Assert.Equal(Caption.MaximumDwell, Caption.DwellFor(new string('x', 400), Caption.AdultReadingSpeed));
    }

    [Fact]
    public void ASlowerReaderGetsLonger()
    {
        var text = new string('x', 60);

        Assert.True(
            Caption.DwellFor(text, Caption.ChildrensReadingSpeed)
            > Caption.DwellFor(text, Caption.AdultReadingSpeed));
    }

    [Fact]
    public void TheWindowHoldsThreeLinesAndRollsTheOldestOff()
    {
        var layer = new CaptionLayer();

        layer.Say("One.", Now);
        layer.Say("Two.", Now);
        layer.Say("Three.", Now);
        layer.Say("Four.", Now);

        Assert.Equal(Caption.WindowLines, layer.Lines.Count);
        Assert.Equal(["Two.", "Three.", "Four."], layer.Lines);
    }

    /// <summary>
    /// The dwell starts when the voice stops, not when it starts. Nobody is reading along with
    /// a voice they can hear — they are catching the last line after it has gone.
    /// </summary>
    [Fact]
    public void CaptionsClearOnTheirOwnAndTheClockStartsWhenTheVoiceStops()
    {
        var layer = new CaptionLayer();

        layer.Say("Fuel is at nineteen per cent.", Now);

        // Ten seconds of talking, which is longer than the longest dwell. Still up: nothing is
        // counting yet.
        layer.Tick(Now + TimeSpan.FromSeconds(10));
        Assert.True(layer.Visible);

        layer.Quiet(Now + TimeSpan.FromSeconds(10));

        layer.Tick(Now + TimeSpan.FromSeconds(11));
        Assert.True(layer.Visible);

        layer.Tick(Now + TimeSpan.FromSeconds(12));
        Assert.False(layer.Visible);
    }

    [Fact]
    public void SayingSomethingElseCancelsThePendingClear()
    {
        var layer = new CaptionLayer();

        layer.Say("First.", Now);
        layer.Quiet(Now);

        layer.Say("Second.", Now + TimeSpan.FromSeconds(0.5));

        // Would have cleared at ~0.83s if the pending clear had survived the new line.
        layer.Tick(Now + TimeSpan.FromSeconds(1));
        Assert.True(layer.Visible);
    }

    /// <summary>
    /// A caption still sitting there after a silence command is d47 visibly not having stopped.
    /// </summary>
    [Fact]
    public void SilenceTakesTheCaptionsWithIt()
    {
        var layer = new CaptionLayer();

        layer.Say("Mid-sentence, and then some more.", Now);
        layer.Silence();

        Assert.False(layer.Visible);
        Assert.Empty(layer.Lines);
    }

    [Fact]
    public void CaptionsSwitchedOffSayNothingAtAll()
    {
        var layer = new CaptionLayer { Settings = new CaptionSettings { Enabled = false } };

        layer.Say("Nothing to see.", Now);

        Assert.False(layer.Visible);
        Assert.Empty(layer.Lines);
    }

    [Fact]
    public void TheWindowOnlyAnnouncesItselfWhenItChanges()
    {
        var layer = new CaptionLayer();
        var changes = 0;
        layer.Changed += () => changes++;

        layer.Say("One.", Now);
        layer.Quiet(Now);

        // Ticks before the dwell is up are not changes, and a redraw per tick would undo the
        // render-only-on-change rule the whole VR path is built on.
        layer.Tick(Now + TimeSpan.FromMilliseconds(100));
        layer.Tick(Now + TimeSpan.FromMilliseconds(200));
        Assert.Equal(1, changes);

        layer.Tick(Now + TimeSpan.FromSeconds(5));
        Assert.Equal(2, changes);

        // And an expiry that has already happened does not keep announcing itself.
        layer.Tick(Now + TimeSpan.FromSeconds(6));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void AHandEditedReadingSpeedIsClampedRatherThanObeyed()
    {
        var absurd = new CaptionSettings { CharactersPerSecond = 0, BackgroundOpacity = 40 }.Sane();

        Assert.True(absurd.CharactersPerSecond >= 8);
        Assert.Equal(1.0, absurd.BackgroundOpacity);
    }

    /// <summary>
    /// One clip is one caption, however many times the audio arbiter mentions it.
    /// <para>
    /// It mentions it constantly. That record is a snapshot of everything audible, re-raised for
    /// every change to any of it — the next sentence queued behind this one, the thinking bed
    /// stopping, a track starting — and each carries the current clip's caption again. Without
    /// an identity to compare, the reported symptom was one line written twice and a three-line
    /// window holding two copies of one sentence
    /// (remediation.md, "Only the first caption arrives").
    /// </para>
    /// </summary>
    [Fact]
    public void TheSameClipReportedAgainIsStillOneCaption()
    {
        var layer = new CaptionLayer();

        layer.Say("Fuel is at nineteen per cent.", Now, utterance: 7);
        var after = layer.Lines.Count;

        layer.Say("Fuel is at nineteen per cent.", Now, utterance: 7);
        layer.Say("Fuel is at nineteen per cent.", Now, utterance: 7);

        Assert.Equal(after, layer.Lines.Count);
    }

    /// <summary>
    /// And the next clip is a new caption even when it says exactly the same thing, which is
    /// why this compares an id rather than the words.
    /// </summary>
    [Fact]
    public void TheSameSentenceSaidTwiceIsTwoCaptions()
    {
        var layer = new CaptionLayer();

        layer.Say("Shields are down.", Now, utterance: 1);
        var after = layer.Lines.Count;

        layer.Say("Shields are down.", Now, utterance: 2);

        Assert.True(layer.Lines.Count > after);
    }

    /// <summary>
    /// A caller with no clip to name still gets a caption. The audition path and the tests are
    /// both in that position, and refusing them would be worse than not de-duplicating.
    /// </summary>
    [Fact]
    public void ACallerWithNoIdentityIsAlwaysANewCaption()
    {
        var layer = new CaptionLayer();

        layer.Say("Interdiction detected.", Now);
        var after = layer.Lines.Count;

        layer.Say("Interdiction detected.", Now);

        Assert.True(layer.Lines.Count > after);
    }

    /// <summary>
    /// Silence forgets which clip was speaking, so the same one starting again after a shut-up
    /// is a caption rather than a repeat that is swallowed.
    /// </summary>
    [Fact]
    public void SilenceForgetsWhatWasBeingSaid()
    {
        var layer = new CaptionLayer();

        layer.Say("Docking granted.", Now, utterance: 3);
        layer.Silence();
        layer.Say("Docking granted.", Now, utterance: 3);

        Assert.NotEmpty(layer.Lines);
    }

    /// <summary>
    /// The dwell is timed against everything still on screen, not only the sentence that just
    /// finished (remediation.md, "Captions: which standard, and how long do they stay?").
    /// <para>
    /// The window is a roll-up, so what a reader is catching up on when the voice stops is the
    /// three lines in front of them. Timing a short last line on its own put the whole window
    /// away after five sixths of a second, which is what "it disappears as soon as the voice
    /// completes" was.
    /// </para>
    /// </summary>
    [Fact]
    public void AFullWindowStaysUpLongerThanItsLastLineWould()
    {
        var full = new CaptionLayer();
        full.Say("Interdiction detected.", Now, utterance: 1);
        full.Say("Submit or run, Commander, but decide now, because the tether is closing.", Now, utterance: 2);
        full.Quiet(Now);

        var alone = new CaptionLayer();
        alone.Say("Interdiction detected.", Now, utterance: 1);
        alone.Quiet(Now);

        // Both are still on screen at the point the short one on its own would have cleared.
        var shortDwell = Caption.DwellFor("Interdiction detected.", Caption.AdultReadingSpeed);

        alone.Tick(Now + shortDwell);
        Assert.Empty(alone.Lines);

        full.Tick(Now + shortDwell);
        Assert.NotEmpty(full.Lines);
    }

    /// <summary>And still inside the standard's ceiling, which is where the argument ends.</summary>
    [Fact]
    public void ItStillClearsWithinTheStandardsMaximum()
    {
        var layer = new CaptionLayer();

        layer.Say("Submit or run, Commander, but decide now, because the tether is closing.", Now, utterance: 1);
        layer.Say("The interdiction tether is at ninety per cent and climbing steadily.", Now, utterance: 2);
        layer.Quiet(Now);

        layer.Tick(Now + Caption.MaximumDwell);

        Assert.Empty(layer.Lines);
    }
}
