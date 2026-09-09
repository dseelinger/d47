using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Delivery direction goes to the voice and nowhere else.</summary>
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
    /// Prose that merely contains a bracket survives, the same rule <see cref="PlainSpeech"/> holds
    /// itself to.
    /// </summary>
    [Theory]
    [InlineData("The contact is at [2] on the scanner.")]
    [InlineData("Reading the value at index [0] now.")]
    [InlineData("A bracket that never closes [ is just a bracket.")]
    public void ProseThatMerelyContainsABracketIsUntouched(string written) =>
        Assert.Equal(written, AudioTags.Strip(written));

    /// <summary>
    /// The collision worth pinning: a markdown link is <c>[text](url)</c>, and taking <c>[text]</c> for
    /// direction would leave the url behind to be read out — which is the exact fault this class exists
    /// to prevent, arriving from the other direction.
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

    /// <summary>What the log line is built from.</summary>
    [Fact]
    public void TheDirectionIsReadableForTheLog()
    {
        Assert.Equal(
            ["alarmed", "reassuring"],
            AudioTags.In("[alarmed] Contact. [reassuring] We have the angle on it."));

        Assert.Empty(AudioTags.In("Contact on the scanner."));
    }

    /// <summary>
    /// A line that is nothing but direction leaves no words behind, and the pipeline drops it rather
    /// than sending a provider an empty string.
    /// </summary>
    [Fact]
    public void ALineOfNothingButDirectionIsEmptyOnceStripped() =>
        Assert.Empty(AudioTags.Strip("[sighs]"));
}
