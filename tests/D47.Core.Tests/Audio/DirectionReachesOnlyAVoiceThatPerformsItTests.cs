using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Delivery direction goes to the voice and nowhere else
/// (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <b>The failure this prevents was measured, not imagined.</b> Flash 2.5 was sent the brackets
/// unchanged and transcribed back as <i>"Whispers, cutting the drives"</i>, <i>"Sighs. That is the
/// third interdiction"</i>, <i>"Sarcastic beautiful landing"</i> — every tag, every time. Kokoro is
/// worse by construction: its phonemiser lists <c>[</c> and <c>]</c> among the brackets it trims,
/// so it pronounces the contents. Four of the five providers are in that position, and only
/// ElevenLabs v3 performs a tag rather than saying it.
/// </para>
/// </summary>
public class DirectionReachesOnlyAVoiceThatPerformsItTests
{
    [Theory]
    [InlineData("[sighs] That is the third interdiction this hour.", "That is the third interdiction this hour.")]
    [InlineData("Hull at 14 percent. [alarmed] Get us down.", "Hull at 14 percent. Get us down.")]
    [InlineData("[strong Scottish accent] Contact on the scanner.", "Contact on the scanner.")]
    [InlineData("[laughs harder] The entire bounty is 812 credits.", "The entire bounty is 812 credits.")]
    public void DirectionComesOutOfTheWrittenLine(string written, string expected) =>
        Assert.Equal(expected, AudioTags.Strip(written));

    /// <summary>
    /// <b>Prose that merely contains a bracket survives</b>, the same rule
    /// <see cref="PlainSpeech"/> holds itself to. A footnote marker, an array index and a
    /// designation in brackets are not direction, and a sentence is not something to be swallowed
    /// by a stray one.
    /// </summary>
    [Theory]
    [InlineData("The contact is at [2] on the scanner.")]
    [InlineData("Reading the value at index [0] now.")]
    [InlineData("A bracket that never closes [ is just a bracket.")]
    public void ProseThatMerelyContainsABracketIsUntouched(string written) =>
        Assert.Equal(written, AudioTags.Strip(written));

    /// <summary>
    /// The collision worth pinning: a markdown link is <c>[text](url)</c>, and taking
    /// <c>[text]</c> for direction would leave the url behind to be read out — which is the exact
    /// fault this class exists to prevent, arriving from the other direction. Links belong to
    /// <see cref="PlainSpeech"/> and this must not touch them.
    /// </summary>
    [Fact]
    public void AMarkdownLinkIsLeftForTheMarkdownStripper()
    {
        const string Written = "See [the route](https://example.test/route) for the detail.";

        Assert.Equal(Written, AudioTags.Strip(Written));
        Assert.Equal("See the route for the detail.", PlainSpeech.Strip(AudioTags.Strip(Written)));
    }

    [Fact]
    public void AVoiceThatPerformsDirectionKeepsIt()
    {
        const string Written = "[sighs] Plotting now, Commander.";

        Assert.Equal(Written, AudioTags.For(Written, performed: true));
        Assert.Equal("Plotting now, Commander.", AudioTags.For(Written, performed: false));
    }

    /// <summary>
    /// What the log line is built from. Every tag, in the order written, so a delivery complaint
    /// can be read against what was asked for.
    /// </summary>
    [Fact]
    public void TheDirectionIsReadableForTheLog()
    {
        Assert.Equal(
            ["alarmed", "reassuring"],
            AudioTags.In("[alarmed] Contact. [reassuring] We have the angle on it."));

        Assert.Empty(AudioTags.In("Contact on the scanner."));
    }

    /// <summary>
    /// A line that is nothing but direction leaves no words behind, and the pipeline drops it
    /// rather than sending a provider an empty string.
    /// </summary>
    [Fact]
    public void ALineOfNothingButDirectionIsEmptyOnceStripped() =>
        Assert.Empty(AudioTags.Strip("[sighs]"));
}
