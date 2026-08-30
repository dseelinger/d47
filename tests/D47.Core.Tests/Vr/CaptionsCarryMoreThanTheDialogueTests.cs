using D47.Core.Audio;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// The parts of the caption standard that are not numbers
/// (<a href="https://github.com/dseelinger/d47/issues/201">#201</a>).
/// <para>
/// <b>Every number was already right, to the digit.</b> Forty-two characters, two lines, twenty
/// and seventeen characters a second, five sixths of a second to seven, the break scored after
/// punctuation and before conjunctions — d47 was built to the spec. What it did not implement was
/// the SDH half: the rules written for a reader who cannot hear, which are about sound events and
/// about who is speaking rather than about how text is laid out.
/// </para>
/// </summary>
public class CaptionsCarryMoreThanTheDialogueTests
{
    /// <summary>
    /// <b>The one sound in d47 that carries safety-relevant meaning.</b> A cue plays immediately
    /// ahead of an urgent callout and says which warning it is before the sentence arrives — so a
    /// hearing Commander gets the marker and then the words, and a reading one used to get only
    /// the words. That is the FCC's "same extent" test failing on the sound it can least afford
    /// to fail on.
    /// </summary>
    [Fact]
    public void EveryAlertCueHasSomethingToWriteDown()
    {
        foreach (var cue in Enum.GetValues<AlertCue>())
        {
            var caption = AlertCues.Caption(cue);

            Assert.StartsWith("[", caption, StringComparison.Ordinal);
            Assert.EndsWith("]", caption, StringComparison.Ordinal);

            // Lowercase, which is the standard's form for a sound event rather than a spoken
            // line — and the enum member's own name leaking out would be the tell that a cue was
            // added and nobody decided what it is called.
            Assert.Equal(caption.ToLowerInvariant(), caption);
            Assert.DoesNotContain(cue.ToString(), caption, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Each cue says its own situation, because each cue <em>is</em> its own situation: a
    /// Commander who has learned the four apart by ear knows which one fired, and a reader is
    /// owed the same. One shared "[alert]" would be the generic warning the cues exist not to be.
    /// </summary>
    [Fact]
    public void NoTwoCuesAreWrittenDownTheSameWay()
    {
        var written = Enum.GetValues<AlertCue>().Select(AlertCues.Caption).ToList();

        Assert.Equal(written.Count, written.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A cue's caption is short enough to be one line, so the marker never pushes the sentence it
    /// is marking off the window it shares.
    /// </summary>
    [Fact]
    public void ACueIsAlwaysOneLine()
    {
        foreach (var cue in Enum.GetValues<AlertCue>())
        {
            Assert.Single(Caption.Wrap(AlertCues.Caption(cue)));
        }
    }

    /// <summary>
    /// <b>d47 is the speaker the caption band belongs to, and everyone else has to be named.</b>
    /// Netflix's SDH rule is a speaker ID when the speaker cannot be visually identified, and in a
    /// headset nobody can — there is no face and no mouth. What settles it is ambiguity rather
    /// than visibility: a label on every one of d47's own lines is the noise the rule exists to
    /// keep out, and the carrier's tower, its captain and a hired crew member are somebody else
    /// in somebody else's voice with nothing else to tell them apart by.
    /// </summary>
    [Fact]
    public void OnlyTheShipsAiGoesUnnamed()
    {
        Assert.Null(VoiceRoles.Called(VoiceRole.ShipAi));

        foreach (var role in Enum.GetValues<VoiceRole>().Where(role => role != VoiceRole.ShipAi))
        {
            Assert.False(string.IsNullOrWhiteSpace(VoiceRoles.Called(role)), $"{role} has no name");
        }
    }

    /// <summary>
    /// <b>0.2 was not a dim caption, it was an invisible one.</b> The clamp existed to stop a
    /// hand-edited value being absurd and permitted the absurd value it was added to prevent:
    /// against a bright scene, <c>#F2F2F2</c> on a box that see-through is about 1.4:1, where
    /// WCAG's floor for normal text is 4.5:1. The new floor clears AA against pure white, which is
    /// the worst case rather than the usual one.
    /// </summary>
    [Fact]
    public void TheBoxCannotBeMadeSoSeeThroughThereIsNothingToRead()
    {
        Assert.Equal(
            Caption.MinimumBackgroundOpacity,
            new CaptionSettings { BackgroundOpacity = 0.2 }.Sane().BackgroundOpacity);

        Assert.True(
            Contrast(Caption.MinimumBackgroundOpacity) >= 4.5,
            $"the floor gives {Contrast(Caption.MinimumBackgroundOpacity):0.0}:1 against white");

        // And the floor is a floor rather than a value: the default is well clear of it and is
        // what a Commander who never touches the row gets.
        Assert.True(new CaptionSettings().BackgroundOpacity > Caption.MinimumBackgroundOpacity);
    }

    /// <summary>
    /// The old floor is kept here as the thing that must not come back. An assertion that only
    /// says "the current number is fine" passes again the moment somebody lowers it.
    /// </summary>
    [Fact]
    public void TheFloorThatWasThereBeforeWouldStillFail()
    {
        Assert.True(Contrast(0.2) < 2.0, "the arithmetic behind the change no longer reproduces");
    }

    /// <summary>
    /// WCAG contrast between the caption text and a black box at this opacity over a white scene
    /// — a station floodlight, an ice ring, a hangar wall. White because that is where a
    /// see-through box does its worst; against a starfield anything reads.
    /// </summary>
    private static double Contrast(double opacity)
    {
        var backdrop = Luminance(1.0 - opacity);
        var text = Luminance(0xF2 / 255.0);

        return (Math.Max(text, backdrop) + 0.05) / (Math.Min(text, backdrop) + 0.05);
    }

    /// <summary>WCAG relative luminance of a grey, sRGB in and linear out.</summary>
    private static double Luminance(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
