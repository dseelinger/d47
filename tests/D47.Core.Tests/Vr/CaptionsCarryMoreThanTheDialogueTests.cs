using D47.Core.Audio;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>The parts of the caption standard that are not numbers.</summary>
public class CaptionsCarryMoreThanTheDialogueTests
{
    /// <summary>The one sound in d47 that carries safety-relevant meaning.</summary>
    [Fact]
    public void EveryAlertCueHasSomethingToWriteDown()
    {
        foreach (var cue in Enum.GetValues<AlertCue>())
        {
            var caption = AlertCues.Caption(cue);

            Assert.StartsWith("[", caption, StringComparison.Ordinal);
            Assert.EndsWith("]", caption, StringComparison.Ordinal);

            // Lowercase, which is the standard's form for a sound event rather than a spoken line — and the
            // enum member's own name leaking out would be the tell that a cue was added and nobody decided
            // what it is called.
            Assert.Equal(caption.ToLowerInvariant(), caption);
            Assert.DoesNotContain(cue.ToString(), caption, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Each cue says its own situation, because each cue is its own situation: a Commander who has
    /// learned the four apart by ear knows which one fired, and a reader is owed the same.
    /// </summary>
    [Fact]
    public void NoTwoCuesAreWrittenDownTheSameWay()
    {
        var written = Enum.GetValues<AlertCue>().Select(AlertCues.Caption).ToList();

        Assert.Equal(written.Count, written.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A cue's caption is short enough to be one line, so the marker never pushes the sentence it is
    /// marking off the window it shares.
    /// </summary>
    [Fact]
    public void ACueIsAlwaysOneLine()
    {
        foreach (var cue in Enum.GetValues<AlertCue>())
        {
            Assert.Single(Caption.Wrap(AlertCues.Caption(cue)));
        }
    }

    /// <summary>d47 is the speaker the caption band belongs to, and everyone else has to be named.</summary>
    [Fact]
    public void OnlyTheShipsAiGoesUnnamed()
    {
        Assert.Null(VoiceRoles.Called(VoiceRole.ShipAi));

        foreach (var role in Enum.GetValues<VoiceRole>().Where(role => role != VoiceRole.ShipAi))
        {
            Assert.False(string.IsNullOrWhiteSpace(VoiceRoles.Called(role)), $"{role} has no name");
        }
    }

    /// <summary>0.2 was not a dim caption, it was an invisible one.</summary>
    [Fact]
    public void TheBoxCannotBeMadeSoSeeThroughThereIsNothingToRead()
    {
        Assert.Equal(
            Caption.MinimumBackgroundOpacity,
            new CaptionSettings { BackgroundOpacity = 0.2 }.Sane().BackgroundOpacity);

        Assert.True(
            Contrast(Caption.MinimumBackgroundOpacity) >= 4.5,
            $"the floor gives {Contrast(Caption.MinimumBackgroundOpacity):0.0}:1 against white");

        // And the floor is a floor rather than a value: the default is well clear of it and is what a
        // Commander who never touches the row gets.
        Assert.True(new CaptionSettings().BackgroundOpacity > Caption.MinimumBackgroundOpacity);
    }

    /// <summary>The old floor is kept here as the thing that must not come back.</summary>
    [Fact]
    public void TheFloorThatWasThereBeforeWouldStillFail()
    {
        Assert.True(Contrast(0.2) < 2.0, "the arithmetic behind the change no longer reproduces");
    }

    /// <summary>
    /// WCAG contrast between the caption text and a black box at this opacity over a white scene — a
    /// station floodlight, an ice ring, a hangar wall.
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
